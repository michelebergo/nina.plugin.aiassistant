using FluentAssertions;
using NINA.Plugin.AIAssistant.AI;
using NUnit.Framework;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// The line under the provider selector is the first feedback after a choice.
    /// </summary>
    [TestFixture]
    public class ProviderStatusLineTest {

        [TestCase(AIProviderType.Anthropic)]
        [TestCase(AIProviderType.Google)]
        [TestCase(AIProviderType.Ollama)]
        [TestCase(AIProviderType.OpenAI)]
        [TestCase(AIProviderType.Mistral)]
        public void WorkingProvider_WithMcpOn_ConfirmsEquipmentControl(AIProviderType provider) {
            ProviderStatusLine.Describe(provider, mcpEnabled: true).Should().Contain("Equipment control on");
        }

        [TestCase(AIProviderType.Anthropic)]
        [TestCase(AIProviderType.OpenAI)]
        [TestCase(AIProviderType.Mistral)]
        public void WorkingProvider_WithMcpOff_PointsAtTheSwitch(AIProviderType provider) {
            var line = ProviderStatusLine.Describe(provider, mcpEnabled: false);
            line.Should().Contain("Equipment control available");
            line.Should().Contain("enable it below");
        }

        [Test]
        public void RetiredProvider_SaysRetired() {
            ProviderStatusLine.Describe(AIProviderType.GitHub, mcpEnabled: true).Should().Contain("retired");
        }
    }
}
