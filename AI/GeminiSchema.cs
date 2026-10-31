using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NINA.Plugin.AIAssistant.AI.MCP;

namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// Gemini's function "parameters" is a restricted JSON Schema: one type word per node,
    /// arrays with 'items', objects with 'properties'. This turns what an MCP server wrote
    /// into that shape.
    /// </summary>
    public static class GeminiSchema
    {
        /// <summary>
        /// Build the Gemini "parameters" schema for a tool. Prefers the full original JSON schema
        /// (external tools) so arrays declare 'items' and nested objects keep 'properties'; falls back
        /// to the simple property map for built-in tools.
        /// </summary>
        public static object Parameters(MCPTool tool)
        {
            if (tool.RawInputSchema != null)
            {
                var sanitized = Sanitize(tool.RawInputSchema);
                // Gemini requires the top-level parameters to be an object schema.
                if (!sanitized.ContainsKey("type"))
                    sanitized["type"] = "object";
                return sanitized;
            }

            var properties = new Dictionary<string, object>();
            foreach (var p in tool.InputSchema.Properties)
                properties[p.Key] = Property(p.Value);

            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = tool.InputSchema.Required
            };
        }

        private static Dictionary<string, object> Property(MCPToolParameter param)
        {
            var type = string.IsNullOrEmpty(param.Type) ? "string" : param.Type;
            var d = new Dictionary<string, object> { ["type"] = type };
            if (!string.IsNullOrEmpty(param.Description))
                d["description"] = param.Description!;
            if (param.Enum != null && param.Enum.Count > 0)
                d["enum"] = param.Enum;
            // Gemini requires array parameters to declare their item type.
            if (type.Equals("array", StringComparison.OrdinalIgnoreCase))
                d["items"] = new Dictionary<string, object> { ["type"] = "string" };
            return d;
        }

        /// <summary>
        /// Recursively convert a JSON Schema into a Gemini-compatible schema, keeping only supported
        /// keywords and guaranteeing arrays have 'items' and objects have 'properties'.
        /// </summary>
        public static Dictionary<string, object> Sanitize(JToken? node)
        {
            var result = new Dictionary<string, object>();
            if (node is not JObject obj)
            {
                result["type"] = "string";
                return result;
            }

            // "type" may be a list ("boolean" or "string"); Gemini takes one word.
            var type = obj["type"] == null
                ? (obj["properties"] != null ? "object" : "string")
                : MCPToolSources.NormalizeSchemaType(obj);
            result["type"] = type;

            var desc = obj["description"]?.ToString();
            if (!string.IsNullOrEmpty(desc))
                result["description"] = desc!;

            if (obj["enum"] is JArray enumVals)
                result["enum"] = enumVals.Select(v => v.ToString()).ToList();

            if (type == "object")
            {
                var outProps = new Dictionary<string, object>();
                if (obj["properties"] is JObject props)
                {
                    foreach (var p in props.Properties())
                        outProps[p.Name] = Sanitize(p.Value);
                }
                result["properties"] = outProps;
                if (obj["required"] is JArray req)
                    result["required"] = req.Select(v => v.ToString()).ToList();
            }
            else if (type == "array")
            {
                result["items"] = obj["items"] != null
                    ? Sanitize(obj["items"])
                    : new Dictionary<string, object> { ["type"] = "string" };
            }

            return result;
        }
    }
}
