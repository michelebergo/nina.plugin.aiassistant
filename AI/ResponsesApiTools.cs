using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NINA.Plugin.AIAssistant.AI.MCP;

namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// The wire format of OpenAI's Responses API (/v1/responses), which the newest models
    /// require for tools: flat tool definitions, an "output" list of typed items, function
    /// calls carrying a call_id, results sent back as function_call_output items.
    /// </summary>
    public static class ResponsesApiTools
    {
        public static List<object> BuildTools(IEnumerable<MCPTool> tools)
        {
            return tools.Select(t => (object)new
            {
                type = "function",
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
            }).ToList();
        }

        public static IEnumerable<JToken> OutputItems(JObject response) => response["output"] as JArray ?? new JArray();

        /// <summary>The assistant's words: every output_text part of every message item, joined.</summary>
        public static string Text(JObject response)
        {
            var parts = OutputItems(response)
                .Where(i => i["type"]?.ToString() == "message")
                .SelectMany(i => i["content"] as JArray ?? new JArray())
                .Where(c => c["type"]?.ToString() == "output_text")
                .Select(c => c["text"]?.ToString() ?? string.Empty);
            return string.Join("\n", parts).Trim();
        }

        public static Dictionary<string, object> ParseArguments(JToken? arguments)
        {
            if (arguments == null) return new Dictionary<string, object>();
            if (arguments.Type == JTokenType.Object) return arguments.ToObject<Dictionary<string, object>>() ?? new();
            var text = arguments.ToString();
            if (string.IsNullOrWhiteSpace(text)) return new Dictionary<string, object>();
            try { return JsonConvert.DeserializeObject<Dictionary<string, object>>(text) ?? new(); }
            catch (JsonException) { return new Dictionary<string, object>(); }
        }
    }

    public sealed class ResponsesApiDialect : IToolDialect
    {
        public List<ChatCompletionsToolCall> ParseCalls(JObject response)
        {
            return ResponsesApiTools.OutputItems(response)
                .Where(i => i["type"]?.ToString() == "function_call")
                .Select(i => new ChatCompletionsToolCall
                {
                    Id = i["call_id"]?.ToString() ?? string.Empty,
                    Name = i["name"]?.ToString() ?? string.Empty,
                    Arguments = ResponsesApiTools.ParseArguments(i["arguments"])
                })
                .ToList();
        }

        /// <summary>
        /// Every output item goes back as it came - reasoning items included: a reasoning
        /// model refuses a follow-up whose function call arrives without its reasoning.
        /// </summary>
        public IEnumerable<object> Echo(JObject response) =>
            ResponsesApiTools.OutputItems(response).Select(i => (object)i.ToObject<object>()!);

        public object Result(ChatCompletionsToolCall call, string content) =>
            new { type = "function_call_output", call_id = call.Id, output = content };
    }
}
