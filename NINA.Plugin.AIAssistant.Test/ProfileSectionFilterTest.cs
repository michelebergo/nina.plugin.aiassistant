using FluentAssertions;
using Newtonsoft.Json.Linq;
using NINA.Plugin.AIAssistant.AI.MCP;
using NUnit.Framework;
using System.Linq;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// nina_show_profile returns the whole NINA settings tree. Handed straight to a model with
    /// an eight-thousand-token window that single answer is the end of the conversation, so the
    /// filter exists to let the model ask for the part it needs - and, when it forgets to ask,
    /// to answer with the menu instead of the meal.
    /// </summary>
    [TestFixture]
    public class ProfileSectionFilterTest {

        private static string Envelope(params (string key, int padding)[] sections) {
            var response = new JObject();
            foreach (var (key, padding) in sections) {
                response[key] = new JObject {
                    ["Value"] = new string('v', padding),
                    ["Marker"] = key
                };
            }
            return new JObject {
                ["Response"] = response,
                ["Error"] = "",
                ["StatusCode"] = 200,
                ["Success"] = true
            }.ToString();
        }

        private static string SmallProfile() =>
            Envelope(("AstrometrySettings", 10), ("CameraSettings", 10), ("TelescopeSettings", 10));

        [Test]
        public void NoSectionAsked_AndTheProfileIsSmall_PassesItThrough() {
            var json = SmallProfile();

            ProfileSectionFilter.Apply(json, null).Should().Be(json);
        }

        [Test]
        public void ASectionAsked_KeepsOnlyThatSubtree() {
            var filtered = ProfileSectionFilter.Apply(SmallProfile(), "camera")!;
            var response = JObject.Parse(filtered)["Response"] as JObject;

            response!.Properties().Select(p => p.Name).Should().ContainSingle()
                     .Which.Should().Be("CameraSettings");
            filtered.Should().Contain("CameraSettings");
            filtered.Should().NotContain("TelescopeSettings");
        }

        [Test]
        public void TheEnvelopeSurvivesFiltering() {
            // The caller reads Success out of the envelope; a filter that returned the bare
            // subtree would turn every filtered call into an apparent failure.
            var filtered = JObject.Parse(ProfileSectionFilter.Apply(SmallProfile(), "astrometry")!);

            filtered["Success"]!.Value<bool>().Should().BeTrue();
            filtered["StatusCode"]!.Value<int>().Should().Be(200);
        }

        [TestCase("astrometry", "AstrometrySettings")]
        [TestCase("camera", "CameraSettings")]
        [TestCase("telescope", "TelescopeSettings")]
        [TestCase("ASTROMETRY", "AstrometrySettings")]
        [TestCase("  camera  ", "CameraSettings")]
        public void SectionNamesAreTheUsersWords_NotNinasFieldNames(string asked, string expected) {
            var filtered = ProfileSectionFilter.Apply(SmallProfile(), asked)!;

            filtered.Should().Contain(expected);
        }

        [Test]
        public void AnUnknownSection_AnswersWithTheOnesThatExist() {
            // Silently returning everything would spend the whole context window punishing a
            // typo; an error with no list leaves the model guessing a second time.
            var filtered = ProfileSectionFilter.Apply(SmallProfile(), "flux capacitor")!;

            filtered.Should().Contain("AstrometrySettings");
            filtered.Should().Contain("CameraSettings");
            filtered.ToLowerInvariant().Should().Contain("section");
            filtered.Length.Should().BeLessThan(SmallProfile().Length);
        }

        [Test]
        public void AHugeProfileWithNoSectionAsked_AnswersWithTheMenu() {
            var huge = Envelope(("AstrometrySettings", 6000), ("CameraSettings", 6000));

            var filtered = ProfileSectionFilter.Apply(huge, null)!;

            filtered.Length.Should().BeLessThan(ProfileSectionFilter.PassThroughLimit);
            filtered.Should().Contain("AstrometrySettings");
            filtered.Should().NotContain(new string('v', 6000));
            filtered.ToLowerInvariant().Should().Contain("section");
        }

        [Test]
        public void AHugeProfileWithASectionAsked_ReturnsThatSectionEvenIfItIsLarge() {
            // The model did the right thing; it must get the answer it asked for.
            var huge = Envelope(("AstrometrySettings", 6000), ("CameraSettings", 6000));

            var filtered = ProfileSectionFilter.Apply(huge, "camera")!;

            filtered.Should().Contain("CameraSettings");
            filtered.Should().NotContain("AstrometrySettings");
        }

        [Test]
        public void SomethingThatIsNotJson_IsLeftExactlyAsItCame() {
            // An error page or a truncated body must reach the caller unaltered; a filter that
            // swallows it turns a diagnosable failure into an empty answer.
            const string garbage = "<html>502 Bad Gateway</html>";

            ProfileSectionFilter.Apply(garbage, "camera").Should().Be(garbage);
            ProfileSectionFilter.Apply(garbage, null).Should().Be(garbage);
        }

        [Test]
        public void AResponseWithoutAProfile_IsLeftAlone() {
            const string json = "{\"Response\":\"OK\",\"Success\":true}";

            ProfileSectionFilter.Apply(json, "camera").Should().Be(json);
        }

        [Test]
        public void NullOrEmptyInput_SurvivesUnchanged() {
            ProfileSectionFilter.Apply(null, "camera").Should().BeNull();
            ProfileSectionFilter.Apply("", "camera").Should().Be("");
        }
    }
}
