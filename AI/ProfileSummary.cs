using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NINA.Plugin.AIAssistant.AI {

    /// <summary>
    /// The handful of facts about the user's rig that are worth telling the assistant on every
    /// request. Deliberately a plain record with no NINA types: reading them out of the live
    /// profile is one thing, deciding how they are worded is another, and only the second one
    /// can be tested without NINA running.
    /// </summary>
    public sealed record ProfileFacts {
        public string? ProfileName { get; init; }
        public double? SiteLatitude { get; init; }
        public double? SiteLongitude { get; init; }
        public double? SiteElevation { get; init; }
        public string? TelescopeName { get; init; }
        public double? FocalLengthMm { get; init; }
        public double? FocalRatio { get; init; }
        public string? CameraName { get; init; }
        public double? PixelSizeUm { get; init; }
        public string? ImageFilePath { get; init; }
        public string? SequenceTemplatePath { get; init; }
    }

    /// <summary>
    /// Turns the profile facts into the short block that rides in the system prompt.
    ///
    /// Two properties matter more than the wording. First, every figure is stated only when it
    /// can actually be computed: a profile with no focal length yields no sampling scale rather
    /// than a plausible-looking one, because a number in a system prompt is read as fact and a
    /// wrong one is worse than a missing one. Second, every string arrives from a settings file
    /// the user edits, so it is scrubbed of anything that could turn it into a line of its own -
    /// a telescope name holding a newline would otherwise dictate its own instruction.
    /// </summary>
    public static class ProfileSummary {

        /// <summary>Upper bound on the block, in characters. A context budget, not a style rule.</summary>
        public const int MaxBlockLength = 700;

        /// <summary>Upper bound on the number of lines the block can ever occupy.</summary>
        public const int MaxLines = 8;

        /// <summary>How much of a name (profile, telescope, camera) survives. Part of the
        /// contract: the block's size guarantee rests on these, not on trimming the result.</summary>
        public const int MaxNameLength = 60;

        /// <summary>How much of a path survives.</summary>
        public const int MaxPathLength = 120;

        // Arcseconds per millimetre of focal length, per micrometre of pixel: 206265 / 1000.
        private const double ArcsecPerRadian = 206.265;

        // Plausibility windows. A value outside these is not a rig, it is a corrupt setting, and
        // the block says nothing rather than reciting it.
        private const double MaxFocalLengthMm = 100000;
        private const double MaxPixelSizeUm = 1000;
        private const double MaxFocalRatio = 1000;
        private const double MinElevationM = -500;
        private const double MaxElevationM = 9000;

        public static string? Build(ProfileFacts? facts) {
            if (facts == null) return null;

            var details = new List<string>();

            var site = FormatSite(facts);
            if (site != null) details.Add("  Site: " + site);

            var telescope = FormatTelescope(facts);
            if (telescope != null) details.Add("  Telescope: " + telescope);

            var camera = FormatCamera(facts);
            if (camera != null) details.Add("  Camera: " + camera);

            var images = Clean(facts.ImageFilePath, MaxPathLength);
            if (images != null) details.Add("  Images: " + images);

            var templates = Clean(facts.SequenceTemplatePath, MaxPathLength);
            if (templates != null) details.Add("  Sequence templates: " + templates);

            // A header on its own says only that the plugin knows nothing, which the model can
            // work out for itself and which still costs tokens.
            if (details.Count == 0) return null;

            var name = Clean(facts.ProfileName, MaxNameLength);
            var header = name == null
                ? "ACTIVE NINA PROFILE - authoritative; do not guess these values:"
                : $"ACTIVE NINA PROFILE \"{name}\" - authoritative; do not guess these values:";

            return Assemble(header, details);
        }

        private static string Assemble(string header, List<string> details) {
            // Six lines at most, each already cut to length: the budget is met by the per-field
            // caps, not by trimming the finished block. A clamp here would be unreachable code
            // that makes the budget trivially true - and would therefore hide the day a new
            // field breaks it. What actually holds the bound is the sweep in
            // ProfileSummaryStressTest, which asserts it over twenty thousand profiles.
            return string.Join("\n", new[] { header }.Concat(details));
        }

        private static string? FormatSite(ProfileFacts facts) {
            var lat = Usable(facts.SiteLatitude, -90, 90);
            var lon = Usable(facts.SiteLongitude, -180, 180);
            if (lat == null || lon == null) return null;

            var text = string.Format(CultureInfo.InvariantCulture, "{0:0.00}{1} {2:0.00}{3}",
                Math.Abs(lat.Value), lat.Value < 0 ? "S" : "N",
                Math.Abs(lon.Value), lon.Value < 0 ? "W" : "E");

            var elevation = Usable(facts.SiteElevation, MinElevationM, MaxElevationM);
            if (elevation != null) {
                text += string.Format(CultureInfo.InvariantCulture, ", {0:0} m", elevation.Value);
            }
            return text;
        }

        private static string? FormatTelescope(ProfileFacts facts) {
            var parts = new List<string>();

            var name = Clean(facts.TelescopeName, MaxNameLength);
            if (name != null) parts.Add(name);

            var focal = Positive(facts.FocalLengthMm, MaxFocalLengthMm);
            if (focal != null) {
                parts.Add(string.Format(CultureInfo.InvariantCulture, "{0:0.##} mm", focal.Value));
            }

            var ratio = Positive(facts.FocalRatio, MaxFocalRatio);
            if (ratio != null) {
                parts.Add(string.Format(CultureInfo.InvariantCulture, "f/{0:0.##}", ratio.Value));
            }

            return parts.Count == 0 ? null : string.Join(" ", parts);
        }

        private static string? FormatCamera(ProfileFacts facts) {
            var parts = new List<string>();

            var name = Clean(facts.CameraName, MaxNameLength);
            if (name != null) parts.Add(name);

            var pixel = Positive(facts.PixelSizeUm, MaxPixelSizeUm);
            if (pixel != null) {
                parts.Add(string.Format(CultureInfo.InvariantCulture, "{0:0.##} um", pixel.Value));
            }

            var scale = SamplingScale(facts);
            if (scale != null) {
                parts.Add(string.Format(CultureInfo.InvariantCulture, "-> {0:0.00} arcsec/px", scale.Value));
            }

            return parts.Count == 0 ? null : string.Join(" ", parts);
        }

        /// <summary>
        /// Arcseconds covered by one pixel. Suppressed when either input is unusable, and also
        /// when the result would round to 0.00 or run away: a scale printed as zero is not a
        /// measurement, it is a rounding artefact presented as one.
        /// </summary>
        private static double? SamplingScale(ProfileFacts facts) {
            var focal = Positive(facts.FocalLengthMm, MaxFocalLengthMm);
            var pixel = Positive(facts.PixelSizeUm, MaxPixelSizeUm);
            if (focal == null || pixel == null) return null;

            var scale = ArcsecPerRadian * pixel.Value / focal.Value;
            if (!IsFinite(scale) || scale < 0.005 || scale > 999999) return null;

            return Math.Round(scale, 2);
        }

        private static double? Positive(double? value, double max) =>
            value.HasValue && IsFinite(value.Value) && value.Value > 0 && value.Value <= max
                ? value
                : null;

        private static double? Usable(double? value, double min, double max) =>
            value.HasValue && IsFinite(value.Value) && value.Value >= min && value.Value <= max
                ? value
                : null;

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        /// <summary>
        /// Collapses a settings string to something that can occupy part of one line: control
        /// characters (newlines among them) become spaces, runs of whitespace collapse, and the
        /// result is cut to length. Blank in, nothing out - NINA stores an unset path as an
        /// empty string, and a label with nothing after it reads as a fact that is missing.
        /// </summary>
        private static string? Clean(string? raw, int maxLength) {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var builder = new StringBuilder(Math.Min(raw.Length, maxLength + 1));
            var lastWasSpace = false;

            foreach (var c in raw) {
                var current = char.IsControl(c) || char.IsWhiteSpace(c) ? ' ' : c;
                if (current == ' ') {
                    if (lastWasSpace || builder.Length == 0) continue;
                    lastWasSpace = true;
                } else {
                    lastWasSpace = false;
                }

                builder.Append(current);
                if (builder.Length > maxLength) break;
            }

            var text = builder.ToString().TrimEnd();
            if (text.Length == 0) return null;

            return text.Length <= maxLength
                ? text
                : text.Substring(0, Math.Max(1, maxLength - 3)).TrimEnd() + "...";
        }
    }
}
