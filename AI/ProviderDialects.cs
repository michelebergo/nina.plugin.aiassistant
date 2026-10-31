using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NINA.Plugin.AIAssistant.AI.MCP;

namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// Anthropic's Messages API: tool_use blocks in the content, only when stop_reason says
    /// so; the assistant content replayed as it came; all results of a round bundled into one
    /// user message of tool_result blocks.
    /// </summary>
    public sealed class AnthropicDialect : IToolDialect
    {
        public static List<object> BuildTools(IEnumerable<MCPTool> tools)
        {
            return tools.Select(t => (object)new
            {
                name = t.Name,
                description = t.Description,
                // Claude accepts full JSON Schema, so an external tool's own schema goes through
                // untouched (arrays with items, nested objects); built-ins use the flat map.
                input_schema = t.RawInputSchema != null
                    ? (object)t.RawInputSchema
                    : new
                    {
                        type = t.InputSchema.Type,
                        properties = t.InputSchema.Properties.ToDictionary(
                            p => p.Key,
                            p => new { type = p.Value.Type, description = p.Value.Description }),
                        required = t.InputSchema.Required
                    }
            }).ToList();
        }

        private static IEnumerable<JToken> Blocks(JObject response) => response["content"] as JArray ?? new JArray();

        public List<ChatCompletionsToolCall> ParseCalls(JObject response)
        {
            if (response["stop_reason"]?.ToString() != "tool_use") return new List<ChatCompletionsToolCall>();

            return Blocks(response)
                .Where(b => b["type"]?.ToString() == "tool_use")
                .Select(b => new ChatCompletionsToolCall
                {
                    Id = b["id"]?.ToString() ?? string.Empty,
                    Name = b["name"]?.ToString() ?? string.Empty,
                    Arguments = b["input"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>()
                })
                .ToList();
        }

        public IEnumerable<object> Echo(JObject response)
        {
            var content = Blocks(response)
                .Where(b => b["type"]?.ToString() is "text" or "tool_use")
                .Select(b => b.ToObject<object>()!)
                .ToList();
            return new[] { new { role = "assistant", content } };
        }

        public IEnumerable<object> Results(IReadOnlyList<(ChatCompletionsToolCall Call, string Content)> results)
        {
            var blocks = results.Select(r => (object)new { type = "tool_result", tool_use_id = r.Call.Id, content = r.Content }).ToList();
            return new[] { new { role = "user", content = blocks } };
        }
    }

    /// <summary>
    /// Gemini's generateContent: functionCall parts with a name and no id; the model's parts
    /// replayed as a "model" content; the results of a round bundled into one "user" content
    /// of functionResponse parts, matched by name. Gemini knows no "function" role - it answered
    /// "Role 'function' is not supported" live on 2026-10-31.
    /// </summary>
    public sealed class GeminiDialect : IToolDialect
    {
        private static JArray? Parts(JObject response) =>
            (response["candidates"] as JArray)?.FirstOrDefault()?["content"]?["parts"] as JArray;

        public List<ChatCompletionsToolCall> ParseCalls(JObject response)
        {
            return (Parts(response) ?? new JArray())
                .Where(p => p["functionCall"] != null)
                .Select(p => new ChatCompletionsToolCall
                {
                    Id = string.Empty,
                    Name = p["functionCall"]!["name"]?.ToString() ?? string.Empty,
                    Arguments = p["functionCall"]!["args"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>()
                })
                .ToList();
        }

        public IEnumerable<object> Echo(JObject response)
        {
            var parts = Parts(response);
            if (parts == null) return Enumerable.Empty<object>();
            return new[] { new { role = "model", parts = parts.ToObject<object>()! } };
        }

        public IEnumerable<object> Results(IReadOnlyList<(ChatCompletionsToolCall Call, string Content)> results)
        {
            var parts = results.Select(r => (object)new
            {
                functionResponse = new
                {
                    name = r.Call.Name,
                    response = new { name = r.Call.Name, content = r.Content }
                }
            }).ToList();
            return new[] { new { role = "user", parts } };
        }
    }

    /// <summary>
    /// Ollama's /api/chat: the OpenAI shape without ids - tool_calls under "message" with the
    /// arguments already an object, one role "tool" message per result.
    /// </summary>
    public sealed class OllamaDialect : IToolDialect
    {
        public List<ChatCompletionsToolCall> ParseCalls(JObject response)
        {
            return (response["message"]?["tool_calls"] as JArray ?? new JArray())
                .Select(c => new ChatCompletionsToolCall
                {
                    Id = string.Empty,
                    Name = c["function"]?["name"]?.ToString() ?? string.Empty,
                    Arguments = ResponsesApiTools.ParseArguments(c["function"]?["arguments"])
                })
                .ToList();
        }

        public IEnumerable<object> Echo(JObject response)
        {
            var message = response["message"];
            return new[]
            {
                new
                {
                    role = "assistant",
                    content = message?["content"]?.ToString() ?? string.Empty,
                    tool_calls = message?["tool_calls"]?.ToObject<object>()
                }
            };
        }

        public IEnumerable<object> Results(IReadOnlyList<(ChatCompletionsToolCall Call, string Content)> results) =>
            results.Select(r => (object)new { role = "tool", content = r.Content });
    }
}
