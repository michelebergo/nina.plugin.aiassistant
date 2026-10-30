using NINA.Core.Utility;
using NINA.Profile.Interfaces;
using System;

namespace NINA.Plugin.AIAssistant.AI {

    /// <summary>
    /// Reads the active NINA profile into <see cref="ProfileFacts"/>. Nothing is decided here -
    /// no formatting, no rounding, no judgement about what is worth saying - so that the part
    /// with rules in it stays testable without NINA running. This half is plumbing, and is
    /// verified by running the plugin.
    /// </summary>
    public static class ProfileFactsReader {

        public static ProfileFacts? Read(IProfileService? profileService) {
            var profile = profileService?.ActiveProfile;
            if (profile == null) return null;

            try {
                return new ProfileFacts {
                    ProfileName = profile.Name,
                    SiteLatitude = profile.AstrometrySettings?.Latitude,
                    SiteLongitude = profile.AstrometrySettings?.Longitude,
                    SiteElevation = profile.AstrometrySettings?.Elevation,
                    TelescopeName = profile.TelescopeSettings?.Name,
                    FocalLengthMm = profile.TelescopeSettings?.FocalLength,
                    FocalRatio = profile.TelescopeSettings?.FocalRatio,
                    CameraName = profile.CameraSettings?.Id,
                    PixelSizeUm = profile.CameraSettings?.PixelSize,
                    ImageFilePath = profile.ImageFileSettings?.FilePath,
                    SequenceTemplatePath = profile.SequenceSettings?.TemplatePath
                };
            } catch (Exception ex) {
                // A profile that cannot be read is a reason to say nothing about the rig, never
                // a reason to fail the user's question.
                Logger.Warning($"AI Assistant: could not read the active profile - {ex.Message}");
                return null;
            }
        }
    }
}
