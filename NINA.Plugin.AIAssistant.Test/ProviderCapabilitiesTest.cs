using FluentAssertions;
using NINA.Plugin.AIAssistant.AI;
using NUnit.Framework;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// Which providers can drive NINA used to be stated in three places by hand (the options
    /// panel, the chat's system-prompt choice, the MCP initialisation) and in none of them for
    /// the user. Issue #10: a GPT-4o user bought credits and then learnt from the model itself
    /// that it "cannot access NINA". This is the single statement the others now read.
    /// </summary>
    [TestFixture]
    public class ProviderCapabilitiesTest {

        [TestCase(AIProviderType.Anthropic)]
        [TestCase(AIProviderType.Google)]
        [TestCase(AIProviderType.Ollama)]
        public void ProvidersWithAToolLoop_CanControlEquipment(AIProviderType provider) {
            ProviderCapabilities.CanControlEquipment(provider).Should().BeTrue();
        }

        [TestCase(AIProviderType.OpenAI)]
        [TestCase(AIProviderType.Mistral)]
        [TestCase(AIProviderType.GitHub)]
        public void ProvidersWithoutAToolLoop_CannotControlEquipment(AIProviderType provider) {
            ProviderCapabilities.CanControlEquipment(provider).Should().BeFalse();
        }

        [TestCase(AIProviderType.OpenAI)]
        [TestCase(AIProviderType.Mistral)]
        public void WorkingProvidersWithoutTools_AreChatOnly(AIProviderType provider) {
            ProviderCapabilities.IsChatOnly(provider).Should().BeTrue();
        }

        [Test]
        public void RetiredProvider_IsNotCalledChatOnly() {
            // GitHub Models cannot chat either: calling it "chat only" would promise more than
            // it delivers. Its own provider explains the retirement.
            ProviderCapabilities.IsChatOnly(AIProviderType.GitHub).Should().BeFalse();
        }

        [TestCase(AIProviderType.Anthropic)]
        [TestCase(AIProviderType.Google)]
        [TestCase(AIProviderType.Ollama)]
        public void ToolCapableProviders_AreNotChatOnly(AIProviderType provider) {
            ProviderCapabilities.IsChatOnly(provider).Should().BeFalse();
        }
    }
}
