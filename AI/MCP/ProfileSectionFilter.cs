using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NINA.Plugin.AIAssistant.AI.MCP {

    /// <summary>
    /// Trims the answer of nina_show_profile down to the part that was asked for.
    ///
    /// The endpoint returns the entire NINA settings tree. That is the right thing for an API
    /// and the wrong thing for a model: on a local model with an eight-thousand-token window,
    /// one such answer fills the context and evicts the conversation that prompted it. So the
    /// tool takes a section, and when it is called without one on a profile too large to spend,
    /// the answer is the list of sections rather than a context window burnt to no purpose.
    /// </summary>
    public static class ProfileSectionFilter {

        /// <summary>Beyond this many characters an unfiltered profile is not passed through.</summary>
        public const int PassThroughLimit = 8000;

        /// <summary>
        /// The words a user or a model would actually use, mapped onto NINA's field names.
        /// Anything not listed still resolves if it appears inside a field name.
        /// </summary>
        private static readonly Dictionary<string, string[]> Aliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) {
                ["astrometry"] = new[] { "AstrometrySettings" },
                ["site"] = new[] { "AstrometrySettings" },
                ["location"] = new[] { "AstrometrySettings" },
                ["camera"] = new[] { "CameraSettings" },
                ["telescope"] = new[] { "TelescopeSettings" },
                ["mount"] = new[] { "TelescopeSettings" },
                ["focuser"] = new[] { "FocuserSettings" },
                ["filterwheel"] = new[] { "FilterWheelSettings" },
                ["filters"] = new[] { "FilterWheelSettings" },
                ["guiding"] = new[] { "GuiderSettings" },
                ["guider"] = new[] { "GuiderSettings" },
                ["platesolving"] = new[] { "PlateSolveSettings" },
                ["imaging"] = new[] { "ImageFileSettings", "ImageSettings" },
                ["images"] = new[] { "ImageFileSettings", "ImageSettings" },
                ["paths"] = new[] { "ImageFileSettings", "SequenceSettings" },
                ["sequence"] = new[] { "SequenceSettings" },
                ["dome"] = new[] { "DomeSettings" },
                ["rotator"] = new[] { "RotatorSettings" },
                ["flat"] = new[] { "FlatDeviceSettings" },
                ["weather"] = new[] { "WeatherDataSettings" },
                ["safety"] = new[] { "SafetyMonitorSettings" }
            };

        public static string? Apply(string? responseJson, string? section) {
            if (string.IsNullOrEmpty(responseJson)) return responseJson;

            JObject envelope;
            try {
                envelope = JObject.Parse(responseJson);
            } catch (JsonException) {
                // An error page, a truncated body, anything that is not JSON: hand it back
                // untouched. A filter that swallows a failure turns it into an empty answer.
                return responseJson;
            }

            if (!(envelope["Response"] is JObject profile) || !profile.Properties().Any()) {
                return responseJson;
            }

            var asked = section?.Trim();
            if (string.IsNullOrEmpty(asked)) {
                return responseJson.Length <= PassThroughLimit
                    ? responseJson
                    : Menu(envelope, profile, responseJson.Length,
                           "The full profile is too large to return at once.");
            }

            var keys = Resolve(asked!, profile);
            if (keys.Count == 0) {
                return Menu(envelope, profile, responseJson.Length,
                            $"No profile section matches '{asked}'.");
            }

            var trimmed = new JObject();
            foreach (var key in keys) trimmed[key] = profile[key];

            var result = (JObject)envelope.DeepClone();
            result["Response"] = trimmed;
            return result.ToString(Formatting.None);
        }

        private static List<string> Resolve(string asked, JObject profile) {
            var present = profile.Properties().Select(p => p.Name).ToList();

            if (Aliases.TryGetValue(asked, out var mapped)) {
                var hits = present.Where(p => mapped.Contains(p, StringComparer.OrdinalIgnoreCase)).ToList();
                if (hits.Count > 0) return hits;
            }

            return present
                .Where(p => p.IndexOf(asked, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        /// <summary>
        /// What to say instead of the profile: the sections that exist, and how to ask for one.
        /// Silently returning everything would spend the whole window punishing a typo.
        /// </summary>
        private static string Menu(JObject envelope, JObject profile, int fullLength, string reason) {
            var result = (JObject)envelope.DeepClone();
            result["Response"] = new JObject {
                ["AvailableSections"] = new JArray(profile.Properties().Select(p => p.Name)),
                ["Note"] = $"{reason} It is {fullLength} characters. " +
                           "Call nina_show_profile again with section=<one of AvailableSections>, " +
                           "or a plain word such as astrometry, camera, telescope, imaging or sequence."
            };
            return result.ToString(Formatting.None);
        }
    }
}
