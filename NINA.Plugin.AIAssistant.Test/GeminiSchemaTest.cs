using System.Collections.Generic;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NINA.Plugin.AIAssistant.AI;
using NINA.Plugin.AIAssistant.AI.MCP;
using NUnit.Framework;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// Gemini's parameters schema takes one type word per node. External servers may write a
    /// list - seen live with the sequential-thinking server's "[\"boolean\",\"string\"]", which
    /// Gemini refused as INVALID_ARGUMENT once the full schema was passed through for Claude.
    /// </summary>
    [TestFixture]
    public class GeminiSchemaTest {

        [Test]
        public void ListOfTypes_BecomesOneWord() {
            var schema = JObject.Parse(@"{""type"":""object"",""properties"":{
                ""isRevision"":{""type"":[""boolean"",""string""],""description"":""x""},
                ""count"":{""type"":[""null"",""integer""]}}}");

            var props = (Dictionary<string, object>)GeminiSchema.Sanitize(schema)["properties"];

            ((Dictionary<string, object>)props["isRevision"])["type"].Should().Be("boolean");
            ((Dictionary<string, object>)props["count"])["type"].Should().Be("integer");
        }

        [Test]
        public void MissingType_IsObjectWhenItHasProperties_ElseString() {
            GeminiSchema.Sanitize(JObject.Parse(@"{""properties"":{""a"":{""type"":""string""}}}"))["type"].Should().Be("object");
            GeminiSchema.Sanitize(JObject.Parse(@"{""description"":""free""}"))["type"].Should().Be("string");
            GeminiSchema.Sanitize(JObject.Parse(@"{""anyOf"":[{""type"":""string""},{""type"":""null""}]}"))["type"].Should().Be("string");
        }

        [Test]
        public void ArrayWithoutItems_GetsStringItems() {
            var result = GeminiSchema.Sanitize(JObject.Parse(@"{""type"":""array""}"));
            ((Dictionary<string, object>)result["items"])["type"].Should().Be("string");
        }

        [Test]
        public void NestedObjectKeepsPropertiesAndRequired() {
            var result = GeminiSchema.Sanitize(JObject.Parse(@"{""type"":""object"",""properties"":{
                ""inner"":{""type"":""object"",""properties"":{""n"":{""type"":""number""}},""required"":[""n""]}}}"));

            var inner = (Dictionary<string, object>)((Dictionary<string, object>)result["properties"])["inner"];
            ((Dictionary<string, object>)((Dictionary<string, object>)inner["properties"])["n"])["type"].Should().Be("number");
            ((List<string>)inner["required"]).Should().Equal("n");
        }

        [Test]
        public void ExternalTool_UsesItsRawSchema_BuiltIn_UsesTheFlatMap() {
            var external = new MCPTool {
                Name = "ext",
                InputSchema = new MCPToolInputSchema(),
                RawInputSchema = JObject.Parse(@"{""type"":""object"",""properties"":{""tags"":{""type"":""array"",""items"":{""type"":""integer""}}}}")
            };
            var builtIn = new MCPTool {
                Name = "nina_x",
                InputSchema = new MCPToolInputSchema {
                    Properties = new Dictionary<string, MCPToolParameter> { ["names"] = new MCPToolParameter { Type = "array" } },
                    Required = new List<string> { "names" }
                }
            };

            var ext = (Dictionary<string, object>)GeminiSchema.Parameters(external);
            var extTags = (Dictionary<string, object>)((Dictionary<string, object>)ext["properties"])["tags"];
            ((Dictionary<string, object>)extTags["items"])["type"].Should().Be("integer");

            var flat = (Dictionary<string, object>)GeminiSchema.Parameters(builtIn);
            var names = (Dictionary<string, object>)((Dictionary<string, object>)flat["properties"])["names"];
            ((Dictionary<string, object>)names["items"])["type"].Should().Be("string");
            ((List<string>)flat["required"]).Should().Equal("names");
        }
    }
}
