using FluentAssertions;
using Newtonsoft.Json.Linq;
using NINA.Plugin.AIAssistant.AI.MCP;
using NUnit.Framework;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// External MCP servers describe their parameters in full JSON Schema, where "type" may be
    /// a list ("boolean" or "string") or absent in favour of anyOf. The plugin's tool model
    /// holds one type string, and OpenAI refuses anything that is not a single JSON Schema
    /// type - seen live with the sequential-thinking server's "[\"boolean\",\"string\"]".
    /// </summary>
    [TestFixture]
    public class JsonSchemaTypeTest {

        [Test]
        public void PlainString_PassesThrough() {
            MCPToolSources.NormalizeSchemaType(JObject.Parse(@"{""type"":""integer""}")).Should().Be("integer");
        }

        [Test]
        public void ListOfTypes_TakesTheFirstNonNull() {
            MCPToolSources.NormalizeSchemaType(JObject.Parse(@"{""type"":[""boolean"",""string""]}")).Should().Be("boolean");
            MCPToolSources.NormalizeSchemaType(JObject.Parse(@"{""type"":[""null"",""number""]}")).Should().Be("number");
        }

        [Test]
        public void MissingOrOnlyNull_FallsBackToString() {
            MCPToolSources.NormalizeSchemaType(JObject.Parse(@"{""description"":""x""}")).Should().Be("string");
            MCPToolSources.NormalizeSchemaType(JObject.Parse(@"{""type"":[""null""]}")).Should().Be("string");
            MCPToolSources.NormalizeSchemaType(JObject.Parse(@"{""anyOf"":[{""type"":""string""},{""type"":""null""}]}")).Should().Be("string");
        }

        [Test]
        public void UnknownWord_FallsBackToString() {
            // A server inventing its own type name must not reach the provider as-is.
            MCPToolSources.NormalizeSchemaType(JObject.Parse(@"{""type"":""text""}")).Should().Be("string");
        }
    }
}
