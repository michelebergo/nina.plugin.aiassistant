using FluentAssertions;
using NINA.Plugin.AIAssistant.AI;
using NUnit.Framework;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// What the Test button says when a provider refuses the request.
    ///
    /// This exists because of a real evening lost: an Anthropic key was rejected with a 400
    /// whose body said, in plain English, exactly which header was missing - and the button
    /// read that body, discarded it, and printed "Invalid API key". The key was fine. The rule
    /// this encodes is that the provider's own words reach the user, and that a status code is
    /// never translated into a cause it does not carry.
    /// </summary>
    [TestFixture]
    public class ApiErrorDescriptionTest {

        private const string IdentityLinked =
            "{\"type\":\"error\",\"error\":{\"type\":\"invalid_request_error\",\"message\":" +
            "\"anthropic-workspace-id is required when authenticating with an identity-linked " +
            "API key; send the id of the workspace this request acts in.\"}}";

        [Test]
        public void TheProvidersOwnMessageIsWhatTheUserReads() {
            var described = ApiErrorDescription.Describe(400, IdentityLinked);

            described.Should().Contain("anthropic-workspace-id");
            described.Should().NotContain("Invalid API key");
        }

        [Test]
        public void A400IsNotCalledAnInvalidKey() {
            // 400 says the request was refused. It is not authentication - that is 401 - and
            // saying so sends the user to check the one thing that is not wrong.
            var described = ApiErrorDescription.Describe(400, "{\"error\":{\"message\":\"credit balance is too low\"}}");

            described.Should().Contain("credit balance is too low");
            described.ToLowerInvariant().Should().NotContain("invalid api key");
            described.Should().Contain("400");
        }

        [Test]
        public void A401IsCalledWhatItIs() {
            var described = ApiErrorDescription.Describe(401, "{\"error\":{\"message\":\"invalid x-api-key\"}}");

            described.ToLowerInvariant().Should().Contain("api key");
            described.Should().Contain("401");
        }

        [Test]
        public void A404NamesTheModel() {
            var described = ApiErrorDescription.Describe(404,
                "{\"type\":\"error\",\"error\":{\"type\":\"not_found_error\",\"message\":\"model: claude-3-5-sonnet-20241022\"}}");

            described.ToLowerInvariant().Should().Contain("model");
            described.Should().Contain("claude-3-5-sonnet-20241022");
        }

        [Test]
        public void A429IsAboutQuotaNotCredentials() {
            var described = ApiErrorDescription.Describe(429, "{\"error\":{\"message\":\"rate_limit_error\"}}");

            described.ToLowerInvariant().Should().MatchRegex("rate|quota|limit");
            described.ToLowerInvariant().Should().NotContain("invalid api key");
        }

        [TestCase("{\"error\":{\"message\":\"openai style\"}}", "openai style")]
        [TestCase("{\"error\":{\"message\":\"google style\",\"code\":400}}", "google style")]
        [TestCase("{\"message\":\"bare message\"}", "bare message")]
        [TestCase("{\"error\":\"error as a string\"}", "error as a string")]
        public void TheMessageIsFoundWhateverShapeTheProviderUses(string body, string expected) {
            ApiErrorDescription.Describe(400, body).Should().Contain(expected);
        }

        [Test]
        public void AnEmptyBodyStillProducesSomethingActionable() {
            // A body can be empty; the status code is then all there is, and the user still
            // needs to be told which one it was rather than a blank complaint.
            var described = ApiErrorDescription.Describe(400, "");

            described.Should().Contain("400");
            described.Trim().Should().NotBeEmpty();
        }

        [Test]
        public void ANullBodyDoesNotThrow() {
            ApiErrorDescription.Describe(503, null).Should().Contain("503");
        }

        [Test]
        public void AnHtmlErrorPageIsShownAsText_NotSwallowed() {
            // Corporate proxies and gateways answer with HTML. It is not JSON, but it is the
            // only evidence the user has, so it must not vanish.
            var described = ApiErrorDescription.Describe(502, "<html><body>502 Bad Gateway</body></html>");

            described.Should().Contain("502 Bad Gateway");
        }

        [Test]
        public void AVeryLongBodyIsCutToSomethingReadable() {
            var described = ApiErrorDescription.Describe(400, "{\"error\":{\"message\":\"" + new string('x', 5000) + "\"}}");

            described.Length.Should().BeLessThanOrEqualTo(ApiErrorDescription.MaxLength);
        }

        [Test]
        public void TheMessageStaysOnOneLine() {
            // It is shown in a single TextBlock beside the button; a body full of newlines
            // would push the rest of the options panel around.
            var described = ApiErrorDescription.Describe(400, "{\"error\":{\"message\":\"first\\nsecond\\r\\nthird\"}}");

            described.Should().NotContain("\n");
            described.Should().NotContain("\r");
        }
    }
}
