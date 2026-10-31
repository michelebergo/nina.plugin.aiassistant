using FluentAssertions;
using Newtonsoft.Json.Linq;
using NINA.Plugin.AIAssistant.AI;
using NINA.Plugin.AIAssistant.AI.MCP;
using NUnit.Framework;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// The three spellings that had their own copy of the loop until 2.5.8.0. Each shape here
    /// is the documented one; the live runs of 30/10/2026 confirmed Ollama and, for the
    /// others, the behaviour these dialects reproduce had been in production since 2.4.x.
    /// </summary>
    [TestFixture]
    public class ProviderDialectsTest {

        private static readonly ChatCompletionsToolCall CallA = new() { Id = "toolu_01", Name = "nina_get_status" };
        private static readonly ChatCompletionsToolCall CallB = new() { Id = "toolu_02", Name = "nina_get_camera_info" };

        // ---------- Anthropic ----------

        private const string ClaudeToolUse = @"{
          ""stop_reason"": ""tool_use"",
          ""content"": [
            { ""type"": ""text"", ""text"": ""Let me check."" },
            { ""type"": ""tool_use"", ""id"": ""toolu_01"", ""name"": ""nina_get_status"", ""input"": { ""verbose"": true } }
          ] }";

        [Test]
        public void Anthropic_ParsesToolUseBlocks_OnlyWhenStopReasonSaysSo() {
            var d = new AnthropicDialect();

            var calls = d.ParseCalls(JObject.Parse(ClaudeToolUse));
            calls.Should().ContainSingle();
            calls[0].Id.Should().Be("toolu_01");
            calls[0].Arguments["verbose"].Should().Be(true);

            // Same blocks, but the model said it was done: the text is the answer.
            var endTurn = JObject.Parse(ClaudeToolUse);
            endTurn["stop_reason"] = "end_turn";
            d.ParseCalls(endTurn).Should().BeEmpty();
        }

        [Test]
        public void Anthropic_EchoesTextAndToolUseBlocks_AsOneAssistantMessage() {
            var echo = new AnthropicDialect().Echo(JObject.Parse(ClaudeToolUse)).Select(o => JObject.FromObject(o)).Single();

            echo["role"]!.ToString().Should().Be("assistant");
            echo["content"]!.Should().HaveCount(2);
            echo["content"]![1]!["id"]!.ToString().Should().Be("toolu_01");
        }

        [Test]
        public void Anthropic_BundlesAllResultsIntoOneUserMessage() {
            // Two tool_result blocks in one user turn: two user turns in a row is what the
            // Messages API rejects.
            var msgs = new AnthropicDialect().Results(new[] { (CallA, "ok A"), (CallB, "ok B") }).Select(o => JObject.FromObject(o)).ToList();

            msgs.Should().ContainSingle();
            msgs[0]["role"]!.ToString().Should().Be("user");
            var blocks = msgs[0]["content"]!;
            blocks.Should().HaveCount(2);
            blocks[0]["type"]!.ToString().Should().Be("tool_result");
            blocks[0]["tool_use_id"]!.ToString().Should().Be("toolu_01");
            blocks[1]["content"]!.ToString().Should().Be("ok B");
        }

        [Test]
        public void Anthropic_BuildTools_UsesTheRawSchemaWhenAnExternalToolHasOne() {
            var raw = JObject.Parse(@"{""type"":""object"",""properties"":{""ids"":{""type"":""array"",""items"":{""type"":""string""}}}}");
            var tools = new[] {
                new MCPTool { Name = "builtin", InputSchema = new MCPToolInputSchema { Properties = new() { ["a"] = new MCPToolParameter { Type = "string" } } } },
                new MCPTool { Name = "external", RawInputSchema = raw }
            };

            var built = AnthropicDialect.BuildTools(tools).Select(o => JObject.FromObject(o)).ToList();

            built[0]["input_schema"]!["properties"]!["a"]!["type"]!.ToString().Should().Be("string");
            built[1]["input_schema"]!["properties"]!["ids"]!["items"]!["type"]!.ToString().Should().Be("string");
        }

        // ---------- Gemini ----------

        private const string GeminiCall = @"{ ""candidates"": [ { ""content"": { ""role"": ""model"", ""parts"": [
            { ""functionCall"": { ""name"": ""nina_get_status"", ""args"": { ""verbose"": true } } },
            { ""functionCall"": { ""name"": ""nina_get_camera_info"", ""args"": {} } } ] },
            ""finishReason"": ""STOP"" } ] }";

        [Test]
        public void Gemini_ParsesFunctionCallParts_WithoutIds() {
            var calls = new GeminiDialect().ParseCalls(JObject.Parse(GeminiCall));

            calls.Should().HaveCount(2);
            calls[0].Name.Should().Be("nina_get_status");
            calls[0].Id.Should().BeEmpty();
            calls[0].Arguments["verbose"].Should().Be(true);
        }

        [Test]
        public void Gemini_EchoesThePartsAsAModelContent() {
            var echo = new GeminiDialect().Echo(JObject.Parse(GeminiCall)).Select(o => JObject.FromObject(o)).Single();
            echo["role"]!.ToString().Should().Be("model");
            echo["parts"]!.Should().HaveCount(2);
        }

        [Test]
        public void Gemini_BundlesResultsIntoOneUserContent_MatchedByName() {
            // Gemini has no "function" role: the API refused it live (31/10/2026).
            var msgs = new GeminiDialect().Results(new[] { (CallA, "ok A"), (CallB, "ok B") }).Select(o => JObject.FromObject(o)).ToList();

            msgs.Should().ContainSingle();
            msgs[0]["role"]!.ToString().Should().Be("user");
            var parts = msgs[0]["parts"]!;
            parts.Should().HaveCount(2);
            parts[0]["functionResponse"]!["name"]!.ToString().Should().Be("nina_get_status");
            parts[0]["functionResponse"]!["response"]!["content"]!.ToString().Should().Be("ok A");
        }

        [Test]
        public void Gemini_TextOnlyAnswer_HasNoCalls() {
            var text = JObject.Parse(@"{ ""candidates"": [ { ""content"": { ""parts"": [ { ""text"": ""All good."" } ] } } ] }");
            new GeminiDialect().ParseCalls(text).Should().BeEmpty();
        }

        // ---------- Ollama ----------

        private const string OllamaCall = @"{ ""message"": { ""role"": ""assistant"", ""content"": """",
            ""tool_calls"": [ { ""function"": { ""name"": ""nina_get_status"", ""arguments"": { ""verbose"": true } } } ] },
            ""prompt_eval_count"": 10, ""eval_count"": 5 }";

        [Test]
        public void Ollama_ParsesToolCalls_WithObjectArguments() {
            var calls = new OllamaDialect().ParseCalls(JObject.Parse(OllamaCall));
            calls.Should().ContainSingle();
            calls[0].Name.Should().Be("nina_get_status");
            calls[0].Arguments["verbose"].Should().Be(true);
        }

        [Test]
        public void Ollama_EchoesTheAssistantMessageWithItsToolCalls() {
            var echo = new OllamaDialect().Echo(JObject.Parse(OllamaCall)).Select(o => JObject.FromObject(o)).Single();
            echo["role"]!.ToString().Should().Be("assistant");
            echo["tool_calls"]![0]!["function"]!["name"]!.ToString().Should().Be("nina_get_status");
        }

        [Test]
        public void Ollama_OneToolMessagePerResult_NoIds() {
            var msgs = new OllamaDialect().Results(new[] { (CallA, "ok A"), (CallB, "ok B") }).Select(o => JObject.FromObject(o)).ToList();
            msgs.Should().HaveCount(2);
            msgs.Should().OnlyContain(m => m["role"]!.ToString() == "tool");
            msgs[1]["content"]!.ToString().Should().Be("ok B");
        }
    }
}
