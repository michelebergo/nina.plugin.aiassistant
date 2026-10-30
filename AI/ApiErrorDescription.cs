using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace NINA.Plugin.AIAssistant.AI {

    /// <summary>
    /// Turns a refused HTTP call into something the user can act on.
    ///
    /// The rule is narrow and worth stating: say what the status code actually means, and then
    /// repeat the provider's own words. Every provider explains its refusals in the response
    /// body - which model it does not know, which header is missing, which balance is empty -
    /// and that sentence is almost always the whole answer. Translating a status code into a
    /// cause it does not carry ("400" into "invalid API key") sends the user to check the one
    /// thing that is not wrong.
    /// </summary>
    public static class ApiErrorDescription {

        /// <summary>Upper bound for the message, which is shown on one line next to a button.</summary>
        public const int MaxLength = 320;

        public static string Describe(int statusCode, string? body) {
            var headline = Headline(statusCode);
            var detail = ExtractMessage(body);

            var text = detail == null ? headline : $"{headline} {detail}";
            return text.Length <= MaxLength ? text : text.Substring(0, MaxLength - 3).TrimEnd() + "...";
        }

        private static string Headline(int statusCode) {
            switch (statusCode) {
                case 400: return "Request refused (400) -";
                case 401: return "API key rejected (401) -";
                case 403: return "Not permitted with this API key (403) -";
                case 404: return "Not found - usually the model (404) -";
                case 429: return "Rate limit or quota exceeded (429) -";
                default:
                    return statusCode >= 500
                        ? $"The provider had a problem ({statusCode}) -"
                        : $"The request failed ({statusCode}) -";
            }
        }

        /// <summary>
        /// Digs the explanation out of whatever shape the provider used: Anthropic, OpenAI,
        /// Google and Mistral all nest it under "error", sometimes as an object with a
        /// "message", sometimes as a plain string. Anything that is not JSON is still shown -
        /// an HTML page from a proxy is poor evidence, but it is the only evidence there is.
        /// </summary>
        private static string? ExtractMessage(string? body) {
            if (string.IsNullOrWhiteSpace(body)) return null;

            try {
                var parsed = JToken.Parse(body);

                var message = parsed.SelectToken("error.message")?.ToString()
                              ?? (parsed.SelectToken("error") as JValue)?.ToString()
                              ?? parsed.SelectToken("message")?.ToString();

                if (!string.IsNullOrWhiteSpace(message)) return OneLine(message!);
            } catch (JsonException) {
                // Not JSON. Fall through and show it as it came.
            }

            return OneLine(body!);
        }

        private static string OneLine(string text) {
            var collapsed = new string(text.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
            while (collapsed.Contains("  ")) collapsed = collapsed.Replace("  ", " ");
            return collapsed.Trim();
        }
    }
}
