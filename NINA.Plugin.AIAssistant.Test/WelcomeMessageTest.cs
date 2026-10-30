using FluentAssertions;
using NINA.Plugin.AIAssistant.AI;
using NUnit.Framework;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// The first thing the panel says is the one place a user reads before typing. It must
    /// promise equipment control only when the provider can deliver it and MCP is on.
    /// </summary>
    [TestFixture]
    public class WelcomeMessageTest {

        [TestCase(AIProviderType.Anthropic)]
        [TestCase(AIProviderType.Google)]
        [TestCase(AIProviderType.Ollama)]
        [TestCase(AIProviderType.OpenAI)]
        [TestCase(AIProviderType.Mistral)]
        public void WorkingProvider_WithMcpOn_PromisesEquipmentControl(AIProviderType provider) {
            WelcomeMessage.Build(provider, mcpEnabled: true).Should().Contain("MCP Control Enabled");
        }

        [TestCase(AIProviderType.Anthropic)]
        [TestCase(AIProviderType.OpenAI)]
        [TestCase(AIProviderType.Mistral)]
        public void WorkingProvider_WithMcpOff_PromisesNothingAboutEquipment(AIProviderType provider) {
            WelcomeMessage.Build(provider, mcpEnabled: false).Should().NotContain("MCP Control Enabled");
        }

        [Test]
        public void RetiredProvider_GetsNoPromiseEvenWithMcpOn() {
            WelcomeMessage.Build(AIProviderType.GitHub, mcpEnabled: true).Should().NotContain("MCP Control Enabled");
        }

        [Test]
        public void EveryProvider_KeepsTheCommonIntroduction() {
            foreach (AIProviderType provider in Enum.GetValues(typeof(AIProviderType))) {
                var text = WelcomeMessage.Build(provider, mcpEnabled: true);
                text.Should().StartWith("Hello!");
                text.Should().Contain("Troubleshooting issues");
            }
        }
    }
}
