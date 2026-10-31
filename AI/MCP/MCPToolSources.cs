using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NINA.Core.Utility;
using NINA.Plugin.AIAssistant.MCP;

namespace NINA.Plugin.AIAssistant.AI.MCP
{
    /// <summary>
    /// The two places a tool can come from - the built-in NINA Advanced API bridge and the
    /// external MCP servers - merged into one list for the model and one dispatcher for its
    /// calls. Same rules as the Ollama provider has applied since 2.4.1: built-in names win a
    /// collision, and a call is tried built-in first, external second.
    /// </summary>
    public static class MCPToolSources
    {
        public static async Task<List<MCPTool>> CollectAsync(
            NINAAdvancedAPIClient? builtIn,
            IExternalMCPSource? external,
            string providerName,
            CancellationToken cancellationToken)
        {
            var allTools = new List<MCPTool>();

            if (builtIn != null)
            {
                allTools.AddRange(builtIn.GetAvailableTools());
                Logger.Info($"{providerName}: Added {allTools.Count} built-in NINA API tools");
            }

            if (external != null && external.IsConnected)
            {
                try
                {
                    var externalTools = await external.GetToolsAsync(cancellationToken);
                    foreach (var tool in externalTools)
                    {
                        var toolName = tool["name"]?.ToString();
                        if (string.IsNullOrEmpty(toolName)) continue;

                        if (allTools.Any(t => t.Name == toolName))
                        {
                            Logger.Warning($"{providerName}: Skipping external MCP tool '{toolName}' because a tool with this name already exists");
                            continue;
                        }

                        var properties = tool["inputSchema"]?["properties"]?.ToObject<JObject>();
                        var propDict = new Dictionary<string, MCPToolParameter>();
                        if (properties != null)
                        {
                            foreach (var prop in properties.Properties())
                            {
                                propDict[prop.Name] = new MCPToolParameter
                                {
                                    Type = NormalizeSchemaType(prop.Value),
                                    Description = prop.Value["description"]?.ToString() ?? ""
                                };
                            }
                        }

                        allTools.Add(new MCPTool
                        {
                            Name = toolName,
                            Description = tool["description"]?.ToString() ?? "",
                            InputSchema = new MCPToolInputSchema
                            {
                                Properties = propDict,
                                Required = tool["inputSchema"]?["required"]?.ToObject<List<string>>() ?? new List<string>()
                            },
                            // The full schema as the server wrote it, for providers that accept
                            // JSON Schema whole (Claude) or need arrays' items (Gemini).
                            RawInputSchema = (tool["inputSchema"] as JObject)?.DeepClone() as JObject
                        });
                    }
                    Logger.Info($"{providerName}: Added {externalTools.Count} external MCP tools");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"{providerName}: Failed to get external MCP tools: {ex.Message}");
                }
            }

            return allTools;
        }

        private static readonly HashSet<string> JsonSchemaTypes = new(StringComparer.Ordinal)
        {
            "string", "number", "integer", "boolean", "object", "array"
        };

        /// <summary>
        /// One JSON Schema type word out of whatever an external server wrote. Full JSON Schema
        /// allows a list ("boolean" or "string") or no type at all (anyOf); the providers
        /// accept exactly one word, and OpenAI rejects the request otherwise.
        /// </summary>
        public static string NormalizeSchemaType(JToken property)
        {
            var type = property["type"];
            if (type is JArray list)
            {
                var first = list.Select(t => t.ToString()).FirstOrDefault(t => t != "null" && JsonSchemaTypes.Contains(t));
                return first ?? "string";
            }
            var word = type?.ToString();
            return word != null && JsonSchemaTypes.Contains(word) ? word : "string";
        }

        public static async Task<MCPToolResult> InvokeAsync(
            NINAAdvancedAPIClient? builtIn,
            IExternalMCPSource? external,
            string toolName,
            Dictionary<string, object> arguments,
            CancellationToken cancellationToken)
        {
            MCPToolResult? result = null;

            if (builtIn != null)
            {
                result = await builtIn.InvokeToolAsync(toolName, arguments, cancellationToken);
                if (!result.Success && result.Error?.Contains("Unknown tool") == true)
                {
                    result = null;
                }
            }

            if (result == null && external != null && external.IsConnected)
            {
                try
                {
                    var externalResult = await external.CallToolAsync(toolName, JObject.FromObject(arguments), cancellationToken);
                    result = new MCPToolResult
                    {
                        Success = externalResult["content"] != null,
                        Content = externalResult["content"]?[0]?["text"]?.ToString() ?? externalResult.ToString(),
                        Error = externalResult["error"]?.ToString()
                    };
                    Logger.Info($"[MCP] Used external MCP server for {toolName}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result = new MCPToolResult { Success = false, Error = ex.Message };
                }
            }

            return result ?? new MCPToolResult { Success = false, Error = "Tool not found in any MCP source" };
        }
    }
}
