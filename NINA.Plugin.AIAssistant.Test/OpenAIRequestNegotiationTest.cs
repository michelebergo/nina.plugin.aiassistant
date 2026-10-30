using FluentAssertions;
using NINA.Plugin.AIAssistant.AI;
using NUnit.Framework;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// OpenAI's models disagree about which parameters they accept, and the set changes with
    /// every generation: gpt-4o wants max_tokens and a temperature, o-series want
    /// max_completion_tokens and no temperature, gpt-6-astra wants reasoning_effort set to
    /// none before it will call a tool. The plugin cannot know the rules of a model released
    /// after it shipped; what it can do is read the 400 the model sends back and fix the one
    /// parameter it names. Each case here is a refusal seen live.
    /// </summary>
    [TestFixture]
    public class OpenAIRequestNegotiationTest {

        private static Dictionary<string, object> Body(bool withTools = false) {
            var b = new Dictionary<string, object> { ["model"] = "x", ["messages"] = new List<object>(), ["temperature"] = 0.7, ["max_tokens"] = 1024 };
            if (withTools) b["tools"] = new List<object>();
            return b;
        }

        [Test]
        public void TemperatureUnsupported_DropsTemperature() {
            var body = Body();
            var changed = OpenAIRequestNegotiation.Adjust(body, 1024, @"{""error"":{""message"":""Unsupported value: 'temperature' does not support 0.7 with this model."",""code"":""unsupported_value"",""param"":""temperature""}}");

            changed.Should().BeTrue();
            body.Should().NotContainKey("temperature");
        }

        [Test]
        public void MaxCompletionTokensRequired_SwapsTheTokenParameter() {
            var body = Body();
            var changed = OpenAIRequestNegotiation.Adjust(body, 1024, @"{""error"":{""message"":""Unsupported parameter: 'max_tokens' is not supported with this model. Use 'max_completion_tokens' instead.""}}");

            changed.Should().BeTrue();
            body.Should().NotContainKey("max_tokens");
            body["max_completion_tokens"].Should().Be(1024);
        }

        [Test]
        public void ToolsNeedReasoningEffortNone_AddsIt() {
            // gpt-6-astra, 30/10/2026: "Function tools with reasoning_effort are not supported ...
            // To use function tools, use /v1/responses or set reasoning_effort to 'none'."
            var body = Body(withTools: true);
            var changed = OpenAIRequestNegotiation.Adjust(body, 1024, @"{""error"":{""message"":""Function tools with reasoning_effort are not supported for gpt-6-astra in /v1/chat/completions. To use function tools, use /v1/responses or set reasoning_effort to 'none'."",""param"":""reasoning_effort""}}");

            changed.Should().BeTrue();
            body["reasoning_effort"].Should().Be("none");
        }

        [Test]
        public void ReasoningEffortRejectedAsUnknown_RemovesIt() {
            var body = Body();
            body["reasoning_effort"] = "none";
            var changed = OpenAIRequestNegotiation.Adjust(body, 1024, @"{""error"":{""message"":""Unrecognized request argument supplied: reasoning_effort""}}");

            changed.Should().BeTrue();
            body.Should().NotContainKey("reasoning_effort");
        }

        [Test]
        public void SameComplaintTwice_IsNotRetriedForever() {
            // Once the parameter has been fixed, the same complaint means the fix did not take:
            // give up rather than loop.
            var body = Body();
            var error = @"{""error"":{""message"":""Unsupported value: 'temperature'"",""param"":""temperature""}}";
            OpenAIRequestNegotiation.Adjust(body, 1024, error).Should().BeTrue();
            OpenAIRequestNegotiation.Adjust(body, 1024, error).Should().BeFalse();
        }

        [Test]
        public void NoneRejectedAfterBeingDemanded_IsNotRepaired_ButExplained() {
            // gpt-6-astra, 30/10/2026, second refusal: "'reasoning_effort' does not support 'none'
            // with this model. Supported values are: 'low', 'medium', 'high', and 'xhigh'." - a
            // model that only calls tools through the Responses API.
            var body = Body(withTools: true);
            body["reasoning_effort"] = "none";
            body["model"] = "gpt-6-astra";
            var error = @"{""error"":{""message"":""Unsupported value: 'reasoning_effort' does not support 'none' with this model. Supported values are: 'low', 'medium', 'high', and 'xhigh'."",""param"":""reasoning_effort"",""code"":""unsupported_value""}}";

            OpenAIRequestNegotiation.Adjust(body, 1024, error).Should().BeFalse("removing it would loop back to the first refusal");
            body["reasoning_effort"].Should().Be("none");

            var explanation = OpenAIRequestNegotiation.ExplainUnrepairable(body, error);
            explanation.Should().Contain("gpt-6-astra").And.Contain("Responses API").And.Contain("gpt-4o");
        }

        [Test]
        public void OrdinaryErrors_HaveNoSpecialExplanation() {
            OpenAIRequestNegotiation.ExplainUnrepairable(Body(withTools: true), @"{""error"":{""message"":""Rate limit reached""}}").Should().BeNull();
        }

        [Test]
        public void UnrelatedError_ChangesNothing() {
            var body = Body();
            var before = new Dictionary<string, object>(body);

            OpenAIRequestNegotiation.Adjust(body, 1024, @"{""error"":{""message"":""Rate limit reached"",""code"":""rate_limit_exceeded""}}").Should().BeFalse();
            OpenAIRequestNegotiation.Adjust(body, 1024, @"{""error"":{""message"":""Incorrect API key provided""}}").Should().BeFalse();

            body.Should().BeEquivalentTo(before);
        }
    }
}
