using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NINA.Plugin.AIAssistant.AI.MCP;

namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// One tool call as the model asked for it on /chat/completions.
    /// </summary>
    public sealed class ChatCompletionsToolCall
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public Dictionary<string, object> Arguments { get; init; } = new();
    }

    /// <summary>
    /// The wire format of tool calling on OpenAI's /chat/completions, which Mistral copies.
    /// Kept apart from the loop so the two things that differ from Ollama - arguments that
    /// arrive as a JSON string, and results that must quote the id of the call they answer -
    /// are each one small function under test.
    /// </summary>
    public static class ChatCompletionsTools
    {
        public static List<object> BuildTools(IEnumerable<MCPTool> tools)
        {
            return tools.Select(t => (object)new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = new
                    {
                        type = t.InputSchema.Type,
                        properties = t.InputSchema.Properties.ToDictionary(
                            p => p.Key,
                            p => new { type = p.Value.Type, description = p.Value.Description }),
                        required = t.InputSchema.Required
                    }
                }
            }).ToList();
        }

        public static JToken? Message(JObject response) => response["choices"]?[0]?["message"];

        public static List<ChatCompletionsToolCall> ParseToolCalls(JObject response)
        {
            var calls = new List<ChatCompletionsToolCall>();
            if (Message(response)?["tool_calls"] is not JArray toolCalls) return calls;

            foreach (var call in toolCalls)
            {
                var function = call["function"];
                calls.Add(new ChatCompletionsToolCall
                {
                    Id = call["id"]?.ToString() ?? string.Empty,
                    Name = function?["name"]?.ToString() ?? string.Empty,
                    Arguments = ParseArguments(function?["arguments"])
                });
            }
            return calls;
        }

        /// <summary>
        /// OpenAI encodes the arguments as a string holding JSON; some compatible servers hand
        /// the object back directly. A model that produced no or broken JSON gets an empty
        /// argument set - the tool then answers with its own validation error, which the model
        /// can read, instead of the whole turn dying on a parse exception.
        /// </summary>
        private static Dictionary<string, object> ParseArguments(JToken? arguments)
        {
            if (arguments == null) return new Dictionary<string, object>();

            if (arguments.Type == JTokenType.Object)
            {
                return arguments.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
            }

            var text = arguments.ToString();
            if (string.IsNullOrWhiteSpace(text)) return new Dictionary<string, object>();

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(text) ?? new Dictionary<string, object>();
            }
            catch (JsonException)
            {
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// The assistant turn that requested the tools, to be replayed before the results: the
        /// API matches each result to a call by id, and refuses results whose call it has not
        /// seen. Content is normalised to a string because null content is legal in the
        /// response but not in every server's request schema.
        /// </summary>
        public static object AssistantEcho(JToken message)
        {
            return new
            {
                role = "assistant",
                content = message["content"]?.Type == JTokenType.String ? message["content"]!.ToString() : string.Empty,
                tool_calls = message["tool_calls"]?.ToObject<object>()
            };
        }

        /// <summary>
        /// A tool result message. OpenAI's schema has no name field on it; Mistral's requires one.
        /// </summary>
        public static object ToolResult(string callId, string toolName, string content, bool includeName)
        {
            if (includeName)
            {
                return new { role = "tool", tool_call_id = callId, name = toolName, content };
            }
            return new { role = "tool", tool_call_id = callId, content };
        }
    }
}
