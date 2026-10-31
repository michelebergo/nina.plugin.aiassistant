using System.Net;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NINA.Plugin.AIAssistant.AI;
using NINA.Plugin.AIAssistant.AI.MCP;
using NUnit.Framework;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// OpenAI's Responses API (/v1/responses): the dialect the newest models insist on for
    /// tools (gpt-6-astra, 30/10/2026, refuses them on chat completions). Flat tool
    /// definitions, an "output" list of typed items instead of choices, function calls with a
    /// call_id, and results sent back as function_call_output items.
    /// </summary>
    [TestFixture]
    public class ResponsesApiToolsTest {

        private static MCPTool StatusTool() => new MCPTool {
            Name = "nina_get_status", Description = "Get equipment status",
            InputSchema = new MCPToolInputSchema { Properties = new Dictionary<string, MCPToolParameter> { ["verbose"] = new MCPToolParameter { Type = "boolean" } } }
        };

        // Shapes as the Responses API documents them.
        private const string CallResponse = @"{
          ""id"": ""resp_1"", ""model"": ""gpt-6-astra"",
          ""output"": [
            { ""type"": ""reasoning"", ""id"": ""rs_1"", ""summary"": [] },
            { ""type"": ""function_call"", ""id"": ""fc_1"", ""call_id"": ""call_abc"", ""name"": ""nina_get_status"", ""arguments"": ""{\""verbose\"": true}"" }
          ],
          ""usage"": { ""input_tokens"": 100, ""output_tokens"": 20, ""total_tokens"": 120 } }";

        private const string TextResponse = @"{
          ""id"": ""resp_2"", ""model"": ""gpt-6-astra"",
          ""output"": [
            { ""type"": ""message"", ""role"": ""assistant"", ""content"": [ { ""type"": ""output_text"", ""text"": ""Camera disconnected, mount connected."" } ] }
          ],
          ""usage"": { ""input_tokens"": 150, ""output_tokens"": 12, ""total_tokens"": 162 } }";

        [Test]
        public void BuildTools_IsFlat_NoFunctionEnvelope() {
            var json = JArray.FromObject(ResponsesApiTools.BuildTools(new[] { StatusTool() }));

            json[0]["type"]!.ToString().Should().Be("function");
            json[0]["name"]!.ToString().Should().Be("nina_get_status");
            json[0]["parameters"]!["properties"]!["verbose"]!["type"]!.ToString().Should().Be("boolean");
            (json[0] as JObject)!.ContainsKey("function").Should().BeFalse();
        }

        [Test]
        public void ParseCalls_ReadsFunctionCallItems_WithTheirCallId() {
            var dialect = new ResponsesApiDialect();
            var calls = dialect.ParseCalls(JObject.Parse(CallResponse));

            calls.Should().ContainSingle();
            calls[0].Id.Should().Be("call_abc");
            calls[0].Name.Should().Be("nina_get_status");
            calls[0].Arguments["verbose"].Should().Be(true);
        }

        [Test]
        public void ParseCalls_TextOnlyResponse_HasNoCalls() {
            new ResponsesApiDialect().ParseCalls(JObject.Parse(TextResponse)).Should().BeEmpty();
        }

        [Test]
        public void Echo_ReplaysEveryOutputItem_ReasoningIncluded() {
            // A reasoning model's function call must travel back together with its reasoning
            // item, or the API refuses the follow-up as incomplete.
            var echo = new ResponsesApiDialect().Echo(JObject.Parse(CallResponse)).Select(o => JObject.FromObject(o)).ToList();

            echo.Should().HaveCount(2);
            echo[0]["type"]!.ToString().Should().Be("reasoning");
            echo[1]["type"]!.ToString().Should().Be("function_call");
            echo[1]["call_id"]!.ToString().Should().Be("call_abc");
        }

        [Test]
        public void Result_IsAFunctionCallOutputItem() {
            var call = new ChatCompletionsToolCall { Id = "call_abc", Name = "nina_get_status" };
            var item = JObject.FromObject(new ResponsesApiDialect().Results(new[] { (call, "all good") }).Single());

            item["type"]!.ToString().Should().Be("function_call_output");
            item["call_id"]!.ToString().Should().Be("call_abc");
            item["output"]!.ToString().Should().Be("all good");
        }

        [Test]
        public void Text_JoinsTheOutputTextParts() {
            ResponsesApiTools.Text(JObject.Parse(TextResponse)).Should().Be("Camera disconnected, mount connected.");
            ResponsesApiTools.Text(JObject.Parse(CallResponse)).Should().BeEmpty();
        }

        [Test]
        public async Task TheGenericLoop_RunsTheResponsesDialect_EndToEnd() {
            var responses = new Queue<(HttpStatusCode, string)>();
            responses.Enqueue((HttpStatusCode.OK, CallResponse));
            responses.Enqueue((HttpStatusCode.OK, TextResponse));
            var sent = new List<List<object>>();
            var invoked = new List<string>();

            var loop = new ToolLoop(
                new ResponsesApiDialect(),
                post: (items, ct) => { sent.Add(new List<object>(items)); return Task.FromResult(responses.Dequeue()); },
                invoke: (name, args, ct) => { invoked.Add(name); return Task.FromResult(new MCPToolResult { Success = true, Content = "ok" }); },
                progress: null, maxIterations: 5);

            var outcome = await loop.RunAsync(new List<object> { new { role = "user", content = "status?" } }, CancellationToken.None);

            outcome.Succeeded.Should().BeTrue();
            invoked.Should().Equal("nina_get_status");
            var second = sent[1].Select(o => JObject.FromObject(o)).ToList();
            // user, reasoning, function_call, function_call_output
            second.Select(i => i["type"]?.ToString() ?? i["role"]!.ToString()).Should().Equal("user", "reasoning", "function_call", "function_call_output");
            ResponsesApiTools.Text(outcome.FinalResponse!).Should().Contain("Camera disconnected");
        }

        [Test]
        public void SuggestsResponsesApi_OnTheRefusalThatNamesIt() {
            OpenAIRequestNegotiation.SuggestsResponsesApi(@"{""error"":{""message"":""Function tools with reasoning_effort are not supported for gpt-6-astra in /v1/chat/completions. To use function tools, use /v1/responses or set reasoning_effort to 'none'.""}}").Should().BeTrue();
            OpenAIRequestNegotiation.SuggestsResponsesApi(@"{""error"":{""message"":""Rate limit reached""}}").Should().BeFalse();
        }
    }
}
