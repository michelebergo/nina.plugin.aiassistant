using System.Net;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NINA.Plugin.AIAssistant.AI;
using NINA.Plugin.AIAssistant.AI.MCP;
using NUnit.Framework;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// The conversation loop that gives OpenAI and Mistral their hands: ask, run what the
    /// model asked for, hand the results back, repeat until it answers in words. Driven here
    /// by scripted responses and a fake tool runner, so every branch is reachable without a
    /// key or a running NINA.
    /// </summary>
    [TestFixture]
    public class ChatCompletionsToolLoopTest {

        private static string ToolCallResponse(params (string id, string name, string args)[] calls) {
            var tc = calls.Select(c => new {
                id = c.id, type = "function",
                function = new { name = c.name, arguments = c.args }
            });
            return JsonConvert.SerializeObject(new {
                choices = new[] { new { message = new { role = "assistant", content = (string?)null, tool_calls = tc } } },
                usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 }
            });
        }

        private static string FinalResponse(string text) => JsonConvert.SerializeObject(new {
            choices = new[] { new { message = new { role = "assistant", content = text } } },
            usage = new { prompt_tokens = 20, completion_tokens = 8, total_tokens = 28 },
            model = "gpt-4o-2024-08-06"
        });

        private sealed class Harness {
            public readonly Queue<(HttpStatusCode, string)> Responses = new();
            public readonly List<List<object>> SentMessages = new();
            public readonly List<(string name, Dictionary<string, object> args)> Invoked = new();
            public readonly List<string> Progress = new();
            public Func<string, Dictionary<string, object>, MCPToolResult> ToolBehaviour =
                (n, a) => new MCPToolResult { Success = true, Content = $"{n} done" };

            public ChatCompletionsToolLoop Loop(bool includeName = false, int maxIterations = 10) =>
                new ChatCompletionsToolLoop(
                    post: (messages, ct) => {
                        SentMessages.Add(new List<object>(messages));
                        return Task.FromResult(Responses.Dequeue());
                    },
                    invoke: (name, args, ct) => {
                        Invoked.Add((name, args));
                        return Task.FromResult(ToolBehaviour(name, args));
                    },
                    progress: Progress.Add,
                    includeToolNameInResult: includeName,
                    maxIterations: maxIterations);
        }

        private static List<object> Seed() => new() {
            new { role = "system", content = "sys" },
            new { role = "user", content = "status?" }
        };

        [Test]
        public async Task NoToolCalls_ReturnsTheFirstAnswerUntouched() {
            var h = new Harness();
            h.Responses.Enqueue((HttpStatusCode.OK, FinalResponse("All connected.")));

            var outcome = await h.Loop().RunAsync(Seed(), CancellationToken.None);

            outcome.Succeeded.Should().BeTrue();
            outcome.FinalResponse!["choices"]![0]!["message"]!["content"]!.ToString().Should().Be("All connected.");
            outcome.ToolResults.Should().BeEmpty();
            h.Invoked.Should().BeEmpty();
            h.SentMessages.Should().HaveCount(1);
        }

        [Test]
        public async Task OneToolCall_IsExecuted_AndItsResultTravelsBackWithTheCallId() {
            var h = new Harness();
            h.Responses.Enqueue((HttpStatusCode.OK, ToolCallResponse(("call_1", "nina_get_status", "{}"))));
            h.Responses.Enqueue((HttpStatusCode.OK, FinalResponse("Camera disconnected, mount connected.")));

            var outcome = await h.Loop().RunAsync(Seed(), CancellationToken.None);

            outcome.Succeeded.Should().BeTrue();
            h.Invoked.Should().ContainSingle().Which.name.Should().Be("nina_get_status");
            outcome.ToolResults.Should().ContainSingle().Which.Should().Be("nina_get_status: Success");

            // Second request = seed + assistant echo + tool result, in that order.
            var second = h.SentMessages[1].Select(m => JObject.FromObject(m)).ToList();
            second.Should().HaveCount(4);
            second[2]["role"]!.ToString().Should().Be("assistant");
            second[2]["tool_calls"]![0]!["id"]!.ToString().Should().Be("call_1");
            second[3]["role"]!.ToString().Should().Be("tool");
            second[3]["tool_call_id"]!.ToString().Should().Be("call_1");
            second[3]["content"]!.ToString().Should().Be("nina_get_status done");
        }

        [Test]
        public async Task ParallelToolCalls_AllRun_AndEachResultKeepsItsOwnId() {
            var h = new Harness();
            h.Responses.Enqueue((HttpStatusCode.OK, ToolCallResponse(("c1", "nina_get_camera_info", "{}"), ("c2", "nina_get_mount_info", "{}"))));
            h.Responses.Enqueue((HttpStatusCode.OK, FinalResponse("done")));

            var outcome = await h.Loop().RunAsync(Seed(), CancellationToken.None);

            outcome.Succeeded.Should().BeTrue();
            h.Invoked.Select(i => i.name).Should().BeEquivalentTo("nina_get_camera_info", "nina_get_mount_info");
            var tools = h.SentMessages[1].Select(m => JObject.FromObject(m)).Where(m => m["role"]!.ToString() == "tool").ToList();
            tools.Select(t => t["tool_call_id"]!.ToString()).Should().BeEquivalentTo("c1", "c2");
        }

        [Test]
        public async Task ArgumentsString_IsDecodedBeforeTheToolSeesIt() {
            var h = new Harness();
            h.Responses.Enqueue((HttpStatusCode.OK, ToolCallResponse(("c1", "nina_slew_mount", "{\"ra\": 10.68, \"dec\": 41.27}"))));
            h.Responses.Enqueue((HttpStatusCode.OK, FinalResponse("slewing")));

            await h.Loop().RunAsync(Seed(), CancellationToken.None);

            h.Invoked.Single().args.Should().ContainKey("ra").And.ContainKey("dec");
        }

        [Test]
        public async Task FailedTool_IsReportedToTheModelAsAnError_NotSwallowed() {
            var h = new Harness();
            h.ToolBehaviour = (n, a) => new MCPToolResult { Success = false, Error = "Camera not connected" };
            h.Responses.Enqueue((HttpStatusCode.OK, ToolCallResponse(("c1", "nina_capture_image", "{}"))));
            h.Responses.Enqueue((HttpStatusCode.OK, FinalResponse("The camera is not connected.")));

            var outcome = await h.Loop().RunAsync(Seed(), CancellationToken.None);

            outcome.Succeeded.Should().BeTrue();
            outcome.ToolResults.Single().Should().Be("nina_capture_image: Failed");
            var toolMsg = h.SentMessages[1].Select(m => JObject.FromObject(m)).Single(m => m["role"]!.ToString() == "tool");
            toolMsg["content"]!.ToString().Should().Contain("Camera not connected");
        }

        [Test]
        public async Task MistralVariant_PutsTheToolNameOnTheResult() {
            var h = new Harness();
            h.Responses.Enqueue((HttpStatusCode.OK, ToolCallResponse(("abc123def", "nina_get_status", "{}"))));
            h.Responses.Enqueue((HttpStatusCode.OK, FinalResponse("ok")));

            await h.Loop(includeName: true).RunAsync(Seed(), CancellationToken.None);

            var toolMsg = h.SentMessages[1].Select(m => JObject.FromObject(m)).Single(m => m["role"]!.ToString() == "tool");
            toolMsg["name"]!.ToString().Should().Be("nina_get_status");
        }

        [Test]
        public async Task ApiError_StopsTheLoop_AndReportsStatusAndBody() {
            var h = new Harness();
            h.Responses.Enqueue(((HttpStatusCode)429, "{\"error\":{\"message\":\"Rate limit reached\"}}"));

            var outcome = await h.Loop().RunAsync(Seed(), CancellationToken.None);

            outcome.Succeeded.Should().BeFalse();
            outcome.Error.Should().Contain("429").And.Contain("Rate limit reached");
            h.Invoked.Should().BeEmpty();
        }

        [Test]
        public async Task ModelThatNeverStopsAskingForTools_HitsTheIterationCap() {
            var h = new Harness();
            for (int i = 0; i < 5; i++)
                h.Responses.Enqueue((HttpStatusCode.OK, ToolCallResponse(($"c{i}", "nina_get_status", "{}"))));

            var outcome = await h.Loop(maxIterations: 3).RunAsync(Seed(), CancellationToken.None);

            outcome.Succeeded.Should().BeFalse();
            outcome.Error.Should().Contain("Maximum tool iterations");
            h.SentMessages.Should().HaveCount(3);
            h.Invoked.Should().HaveCount(3);
        }

        [Test]
        public async Task Cancellation_StopsBeforeTheNextRequest() {
            var h = new Harness();
            var cts = new CancellationTokenSource();
            h.ToolBehaviour = (n, a) => { cts.Cancel(); return new MCPToolResult { Success = true, Content = "ok" }; };
            h.Responses.Enqueue((HttpStatusCode.OK, ToolCallResponse(("c1", "nina_get_status", "{}"))));
            h.Responses.Enqueue((HttpStatusCode.OK, FinalResponse("never sent")));

            Func<Task> act = () => h.Loop().RunAsync(Seed(), cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
            h.SentMessages.Should().HaveCount(1);
        }

        [Test]
        public async Task Progress_NamesEachToolAsItIsCalled_AndItsOutcome() {
            var h = new Harness();
            h.Responses.Enqueue((HttpStatusCode.OK, ToolCallResponse(("c1", "nina_get_status", "{}"))));
            h.Responses.Enqueue((HttpStatusCode.OK, FinalResponse("ok")));

            await h.Loop().RunAsync(Seed(), CancellationToken.None);

            h.Progress.Should().Contain(p => p.Contains("Calling") && p.Contains("nina_get_status"));
            h.Progress.Count.Should().BeGreaterThanOrEqualTo(3, "iteration, call, outcome");
        }
    }
}
