using System;
using System.Collections.Generic;
using System.Linq;

namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// What a model can hold and what it costs, for the usage readout in the chat header.
    ///
    /// Both tables are matched by prefix, because vendors version their names ("-20250929",
    /// "-latest") far more often than they change the family's limits. Anything unknown is
    /// reported as unknown rather than guessed: an invented context size would put a
    /// reassuring percentage next to a conversation that is about to be truncated.
    ///
    /// Figures reviewed 2026-08-16. Prices drift, so the cost readout is labelled as an
    /// estimate and simply disappears for models this table does not know.
    /// </summary>
    public static class ModelLimits
    {
        /// <summary>Context window in tokens, by model-name prefix.</summary>
        private static readonly (string Prefix, int Window)[] ContextWindows =
        {
            ("claude-", 200_000),
            ("gpt-4o", 128_000),
            ("gpt-4.1", 1_000_000),
            ("gpt-5", 400_000),
            ("o1", 200_000),
            ("o3", 200_000),
            ("o4", 200_000),
            ("gemini-1.5", 1_000_000),
            ("gemini-2", 1_000_000),
            ("gemini-flash", 1_000_000),
            ("gemini-pro", 1_000_000),
            ("mistral-large", 128_000),
            ("mistral-medium", 128_000),
            ("mistral-small", 128_000),
        };

        /// <summary>US dollars per million tokens (input, output), by model-name prefix.</summary>
        private static readonly (string Prefix, double In, double Out)[] Prices =
        {
            ("claude-opus", 15.00, 75.00),
            ("claude-sonnet", 3.00, 15.00),
            ("claude-3-5-sonnet", 3.00, 15.00),
            ("claude-haiku", 0.80, 4.00),
            ("claude-3-5-haiku", 0.80, 4.00),
            ("gpt-4o-mini", 0.15, 0.60),
            ("gpt-4o", 2.50, 10.00),
            ("gemini-flash", 0.075, 0.30),
            ("gemini-1.5-flash", 0.075, 0.30),
            ("gemini-1.5-pro", 1.25, 5.00),
            ("mistral-large", 2.00, 6.00),
            ("mistral-small", 0.20, 0.60),
        };

        /// <summary>
        /// The context window of a model, or null when it is not known. Local models are
        /// deliberately absent: their window is set by whoever built the model file, so any
        /// number here would be a guess about someone else's configuration.
        /// </summary>
        public static int? ContextWindowFor(string? modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId)) { return null; }

            var id = modelId.Trim().ToLowerInvariant();
            var match = ContextWindows
                .Where(w => id.StartsWith(w.Prefix, StringComparison.Ordinal))
                .OrderByDescending(w => w.Prefix.Length)
                .FirstOrDefault();

            return match.Prefix == null ? (int?)null : match.Window;
        }

        /// <summary>Estimated cost in US dollars, or null when the model's price is not known.</summary>
        public static double? EstimateCost(string? modelId, long inputTokens, long outputTokens)
        {
            if (string.IsNullOrWhiteSpace(modelId)) { return null; }

            var id = modelId.Trim().ToLowerInvariant();
            var match = Prices
                .Where(p => id.StartsWith(p.Prefix, StringComparison.Ordinal))
                .OrderByDescending(p => p.Prefix.Length)
                .FirstOrDefault();

            if (match.Prefix == null) { return null; }

            return inputTokens / 1_000_000.0 * match.In + outputTokens / 1_000_000.0 * match.Out;
        }

        /// <summary>Compact token count: 950, 12.4k, 1.03M.</summary>
        public static string FormatTokens(long tokens)
        {
            if (tokens < 1_000) { return tokens.ToString(); }
            if (tokens < 1_000_000) { return $"{tokens / 1_000.0:0.#}k"; }
            return $"{tokens / 1_000_000.0:0.##}M";
        }
    }
}
