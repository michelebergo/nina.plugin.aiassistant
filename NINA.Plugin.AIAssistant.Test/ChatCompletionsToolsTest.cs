using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NINA.Plugin.AIAssistant.AI;
using NINA.Plugin.AIAssistant.AI.MCP;
using NUnit.Framework;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// The wire format of tool calling on /chat/completions (OpenAI, and Mistral, which copies
    /// it). Two traps that Ollama's loop never had to face: the model's arguments arrive as a
    /// JSON *string*, not an object, and every tool result must carry the id of the call it
    /// answers or the API refuses the whole conversation.
    /// </summary>
    [TestFixture]
    public class ChatCompletionsToolsTest {

        private static MCPTool StatusTool() => new MCPTool {
            Name = "nina_get_status",
            Description = "Get equipment status",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolParameter> {
                    ["verbose"] = new MCPToolParameter { Type = "boolean", Description = "More detail" }
                },
                Required = new List<string>()
            }
        };

        [Test]
        public void BuildTools_UsesTheFunctionEnvelope() {
            var tools = ChatCompletionsTools.BuildTools(new[] { StatusTool() });
            var json = JArray.FromObject(tools);

            json.Should().HaveCount(1);
            json[0]["type"]!.ToString().Should().Be("function");
            json[0]["function"]!["name"]!.ToString().Should().Be("nina_get_status");
            json[0]["function"]!["parameters"]!["type"]!.ToString().Should().Be("object");
            json[0]["function"]!["parameters"]!["properties"]!["verbose"]!["type"]!.ToString().Should().Be("boolean");
        }

        [Test]
        public void ParseToolCalls_ReadsArgumentsEncodedAsAString() {
            // Verbatim shape of an OpenAI response: "arguments" is a string holding JSON.
            var response = JObject.Parse(@"{
              ""choices"": [ { ""message"": {
                ""role"": ""assistant"", ""content"": null,
                ""tool_calls"": [ { ""id"": ""call_abc123"", ""type"": ""function"",
                  ""function"": { ""name"": ""nina_slew_mount"", ""arguments"": ""{\""ra\"": 10.68, \""dec\"": 41.27}"" } } ]
              } } ] }");

            var calls = ChatCompletionsTools.ParseToolCalls(response);

            calls.Should().HaveCount(1);
            calls[0].Id.Should().Be("call_abc123");
            calls[0].Name.Should().Be("nina_slew_mount");
            calls[0].Arguments.Should().ContainKey("ra").And.ContainKey("dec");
            Convert.ToDouble(calls[0].Arguments["ra"]).Should().BeApproximately(10.68, 1e-9);
        }

        [Test]
        public void ParseToolCalls_ToleratesArgumentsAlreadyAnObject() {
            // Some OpenAI-compatible servers hand the object back directly, as Ollama does.
            var response = JObject.Parse(@"{ ""choices"": [ { ""message"": { ""tool_calls"": [
              { ""id"": ""x"", ""function"": { ""name"": ""t"", ""arguments"": { ""a"": 1 } } } ] } } ] }");

            var calls = ChatCompletionsTools.ParseToolCalls(response);

            calls.Single().Arguments.Should().ContainKey("a");
        }

        [Test]
        public void ParseToolCalls_EmptyOrMalformedArguments_YieldNoArgumentsRatherThanAnException() {
            var response = JObject.Parse(@"{ ""choices"": [ { ""message"": { ""tool_calls"": [
              { ""id"": ""x"", ""function"": { ""name"": ""t"", ""arguments"": """" } },
              { ""id"": ""y"", ""function"": { ""name"": ""u"", ""arguments"": ""not json"" } } ] } } ] }");

            var calls = ChatCompletionsTools.ParseToolCalls(response);

            calls.Should().HaveCount(2);
            calls.Should().OnlyContain(c => c.Arguments.Count == 0);
        }

        [Test]
        public void ParseToolCalls_NoToolCalls_ReturnsEmpty() {
            var response = JObject.Parse(@"{ ""choices"": [ { ""message"": { ""role"": ""assistant"", ""content"": ""All connected."" } } ] }");

            ChatCompletionsTools.ParseToolCalls(response).Should().BeEmpty();
        }

        [Test]
        public void AssistantEcho_CarriesTheToolCallsBackVerbatim() {
            // The assistant turn that asked for the tools must be in the history when the
            // results are sent, with the same ids, or the API cannot match them.
            var message = JObject.Parse(@"{ ""role"": ""assistant"", ""content"": null,
              ""tool_calls"": [ { ""id"": ""call_1"", ""type"": ""function"", ""function"": { ""name"": ""t"", ""arguments"": ""{}"" } } ] }");

            var echo = JObject.FromObject(ChatCompletionsTools.AssistantEcho(message));

            echo["role"]!.ToString().Should().Be("assistant");
            echo["tool_calls"]![0]!["id"]!.ToString().Should().Be("call_1");
            // null content serialises as "" so the field is always present and a string.
            echo["content"]!.Type.Should().Be(JTokenType.String);
        }

        [Test]
        public void ToolResult_CarriesTheCallId_AndTheNameOnlyWhenAsked() {
            var plain = JObject.FromObject(ChatCompletionsTools.ToolResult("call_1", "nina_get_status", "ok", includeName: false));
            var named = JObject.FromObject(ChatCompletionsTools.ToolResult("call_1", "nina_get_status", "ok", includeName: true));

            plain["role"]!.ToString().Should().Be("tool");
            plain["tool_call_id"]!.ToString().Should().Be("call_1");
            plain["content"]!.ToString().Should().Be("ok");
            plain.ContainsKey("name").Should().BeFalse("OpenAI's tool message has no name field");

            named["name"]!.ToString().Should().Be("nina_get_status", "Mistral requires it");
        }
    }
}
