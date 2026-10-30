using FluentAssertions;
using NINA.Plugin.AIAssistant.AI;
using NUnit.Framework;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// The first thing the panel says is the one place a user reads before typing. With MCP
    /// switched on it used to promise equipment control for every provider, including the two
    /// that have no tool loop - so the promise was false exactly for the users who most needed
    /// to know (issue #10).
    /// </summary>
    [TestFixture]
    public class WelcomeMessageTest {

        [TestCase(AIProviderType.Anthropic)]
        [TestCase(AIProviderType.Google)]
        [TestCase(AIProviderType.Ollama)]
        public void ToolCapableProvider_WithMcpOn_PromisesEquipmentControl(AIProviderType provider) {
            var text = WelcomeMessage.Build(provider, mcpEnabled: true);

            text.Should().Contain("MCP Control Enabled");
            text.Should().NotContain("Chat only");
        }

        [TestCase(AIProviderType.Anthropic)]
        [TestCase(AIProviderType.Google)]
        [TestCase(AIProviderType.Ollama)]
        public void ToolCapableProvider_WithMcpOff_PromisesNothingAboutEquipment(AIProviderType provider) {
            var text = WelcomeMessage.Build(provider, mcpEnabled: false);

            text.Should().NotContain("MCP Control Enabled");
            text.Should().NotContain("Chat only");
        }

        [TestCase(AIProviderType.OpenAI, "OpenAI")]
        [TestCase(AIProviderType.Mistral, "Mistral")]
        public void ChatOnlyProvider_WithMcpOn_SaysChatOnlyInsteadOfPromisingControl(AIProviderType provider, string name) {
            // The user left MCP on from a previous provider, or ticked it hoping it would work.
            var text = WelcomeMessage.Build(provider, mcpEnabled: true);

            text.Should().NotContain("MCP Control Enabled");
            text.Should().Contain("Chat only");
            text.Should().Contain(name);
            // Names the way out, so the message is a direction and not just a refusal.
            text.Should().Contain("Anthropic Claude").And.Contain("Google Gemini").And.Contain("Ollama");
        }

        [TestCase(AIProviderType.OpenAI)]
        [TestCase(AIProviderType.Mistral)]
        public void ChatOnlyProvider_WithMcpOff_StillSaysChatOnly(AIProviderType provider) {
            // Whether the user ticked MCP is irrelevant: the provider cannot drive NINA either way,
            // and the point of the line is to say so before any credit is spent.
            WelcomeMessage.Build(provider, mcpEnabled: false).Should().Contain("Chat only");
        }

        [Test]
        public void RetiredProvider_GetsNeitherPromiseNorChatOnlyLine() {
            var text = WelcomeMessage.Build(AIProviderType.GitHub, mcpEnabled: true);

            text.Should().NotContain("MCP Control Enabled");
            text.Should().NotContain("Chat only");
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
