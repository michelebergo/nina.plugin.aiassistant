using System;
using System.Collections.Generic;
using System.Linq;

namespace NINA.Plugin.AIAssistant.AI.MCP
{
    /// <summary>
    /// Formats tool calls for the activity trace shown in the chat.
    ///
    /// The trace is read by a human watching a session, not by a machine, so it answers
    /// two questions the tool name alone cannot: what was it asked to do, and did it work.
    /// "slew to M31" and "slew to M42" are the same tool; a tool that failed and one that
    /// returned an empty result look identical without the outcome line.
    /// </summary>
    public static class MCPToolTrace
    {
        /// <summary>Arguments are context, not payload: enough to recognise the call, never a wall of JSON.</summary>
        private const int MaxArgumentsLength = 90;

        /// <summary>Renders the arguments as a short parenthesis, or nothing when there are none.</summary>
        public static string DescribeArguments(Dictionary<string, object>? arguments)
        {
            if (arguments == null || arguments.Count == 0)
            {
                return string.Empty;
            }

            var parts = arguments
                .Where(a => a.Value != null)
                .Select(a => $"{a.Key}={Shorten(a.Value?.ToString(), 40)}");

            var text = string.Join(", ", parts);
            return string.IsNullOrEmpty(text) ? string.Empty : $" ({Shorten(text, MaxArgumentsLength)})";
        }

        /// <summary>
        /// Renders the outcome. A successful call reports the size of what came back,
        /// because "succeeded with nothing to say" is a real and confusing case; a failed
        /// one reports the reason, which is the line the user actually needs.
        /// </summary>
        public static string DescribeOutcome(bool success, string? content, string? error)
        {
            if (!success)
            {
                return $"   ✗ {Shorten(error, 120) ?? "failed"}";
            }

            var length = content?.Length ?? 0;
            return length == 0 ? "   ✓ ok (empty result)" : $"   ✓ ok ({length} chars)";
        }

        private static string? Shorten(string? value, int max)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var single = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return single.Length <= max ? single : single.Substring(0, max) + "...";
        }
    }
}
