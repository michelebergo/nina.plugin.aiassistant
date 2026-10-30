using System.Collections.Generic;

namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// Reads a 400 from /chat/completions and repairs the one parameter it complains about,
    /// so the request can be sent again. The plugin cannot carry the parameter rules of models
    /// released after it shipped; the model's own refusal is the up-to-date rulebook. Each
    /// repair is applied once: the same complaint after the repair means it did not take.
    /// </summary>
    public static class OpenAIRequestNegotiation
    {
        /// <returns>True when the body was changed and is worth sending again.</returns>
        public static bool Adjust(Dictionary<string, object> body, int maxTokens, string errorBody)
        {
            if (string.IsNullOrEmpty(errorBody)) return false;

            // Reasoning models refuse a custom temperature.
            if (errorBody.Contains("temperature") && body.ContainsKey("temperature")
                && (errorBody.Contains("unsupported_value") || errorBody.Contains("Unsupported value") || errorBody.Contains("does not support")))
            {
                body.Remove("temperature");
                return true;
            }

            // Reasoning models take max_completion_tokens instead of max_tokens.
            if (errorBody.Contains("max_completion_tokens") && body.ContainsKey("max_tokens"))
            {
                body.Remove("max_tokens");
                body.Remove("temperature");
                body["max_completion_tokens"] = maxTokens;
                return true;
            }

            if (errorBody.Contains("reasoning_effort"))
            {
                // "set reasoning_effort to 'none'": tools on a reasoning model, through this endpoint.
                if (errorBody.Contains("'none'") && !body.ContainsKey("reasoning_effort"))
                {
                    body["reasoning_effort"] = "none";
                    return true;
                }
                // "Unrecognized request argument": a model that has no reasoning to switch off.
                if (body.ContainsKey("reasoning_effort") && !errorBody.Contains("'none'"))
                {
                    body.Remove("reasoning_effort");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The model names the other endpoint: "To use function tools, use /v1/responses".
        /// </summary>
        public static bool SuggestsResponsesApi(string errorBody) =>
            !string.IsNullOrEmpty(errorBody) && errorBody.Contains("/v1/responses");

        /// <summary>
        /// The refusal that no repair can answer: the model demands reasoning_effort 'none'
        /// for tools on /chat/completions and then rejects 'none'. Such a model calls tools
        /// only through the Responses API. Returns a sentence for the user, or null.
        /// </summary>
        public static string? ExplainUnrepairable(Dictionary<string, object> body, string errorBody)
        {
            if (errorBody.Contains("reasoning_effort") && errorBody.Contains("'none'")
                && body.TryGetValue("reasoning_effort", out var effort) && Equals(effort, "none")
                && body.ContainsKey("tools"))
            {
                var model = body.TryGetValue("model", out var m) ? m?.ToString() : "This model";
                return $"{model} only calls tools through OpenAI's Responses API, which this plugin does not use yet. " +
                       "For equipment control pick a model that supports tools on chat completions - gpt-4o, gpt-4o-mini or gpt-4.1 - " +
                       "or turn MCP off to chat with this model without tools.";
            }
            return null;
        }
    }
}
