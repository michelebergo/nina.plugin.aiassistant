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
        [TestCase(AIProviderType.OpenAI)]
        [TestCase(AIProviderType.Mistral)]
        public void EveryWorkingProvider_CanControlEquipment(AIProviderType provider) {
            ProviderCapabilities.CanControlEquipment(provider).Should().BeTrue();
        }

        [Test]
        public void RetiredProvider_CannotControlEquipment() {
            ProviderCapabilities.CanControlEquipment(AIProviderType.GitHub).Should().BeFalse();
        }
    }
}
