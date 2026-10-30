using FluentAssertions;
using NINA.Plugin.AIAssistant.AI;
using NUnit.Framework;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// The line under the provider selector is the first feedback after a choice. It used to
    /// say "Using OpenAI API" - true, and silent about the one thing that mattered.
    /// </summary>
    [TestFixture]
    public class ProviderStatusLineTest {

        [TestCase(AIProviderType.Anthropic)]
        [TestCase(AIProviderType.Google)]
        [TestCase(AIProviderType.Ollama)]
        public void ToolCapableProvider_WithMcpOn_ConfirmsEquipmentControl(AIProviderType provider) {
            var line = ProviderStatusLine.Describe(provider, mcpEnabled: true);
            line.Should().Contain("Equipment control on");
            line.Should().NotContain("Chat only");
        }

        [TestCase(AIProviderType.Anthropic)]
        [TestCase(AIProviderType.Google)]
        [TestCase(AIProviderType.Ollama)]
        public void ToolCapableProvider_WithMcpOff_PointsAtTheSwitch(AIProviderType provider) {
            var line = ProviderStatusLine.Describe(provider, mcpEnabled: false);
            line.Should().Contain("Equipment control available");
            line.Should().Contain("enable it below");
        }

        [TestCase(AIProviderType.OpenAI, true)]
        [TestCase(AIProviderType.OpenAI, false)]
        [TestCase(AIProviderType.Mistral, true)]
        [TestCase(AIProviderType.Mistral, false)]
        public void ChatOnlyProvider_SaysChatOnlyWhateverTheMcpSwitch(AIProviderType provider, bool mcpEnabled) {
            var line = ProviderStatusLine.Describe(provider, mcpEnabled);
            line.Should().Contain("Chat only");
            line.Should().NotContain("Equipment control on");
        }

        [Test]
        public void RetiredProvider_SaysRetired() {
            ProviderStatusLine.Describe(AIProviderType.GitHub, mcpEnabled: true).Should().Contain("retired");
        }
    }
}
