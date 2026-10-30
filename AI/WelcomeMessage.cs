namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// The first message in the panel. Kept out of the view model so it can be tested: it is
    /// the one line a user reads before spending credit, and it used to promise equipment
    /// control for every provider whenever MCP was ticked - including, before 2.5.7.0, the two
    /// that had no tool loop.
    /// </summary>
    public static class WelcomeMessage
    {
        public static string Build(AIProviderType provider, bool mcpEnabled)
        {
            var text = "Hello! I'm your AI assistant for astrophotography. Ask me anything about:\n\n" +
                       "• Equipment settings and optimization\n" +
                       "• Target selection and planning\n" +
                       "• Image processing tips\n" +
                       "• Troubleshooting issues\n\n";

            if (mcpEnabled && ProviderCapabilities.CanControlEquipment(provider))
            {
                text += "🤖 **MCP Control Enabled** - I can directly control your NINA equipment!\n" +
                        "Try: \"Connect to NINA\", \"Show equipment status\", \"Take a 10s exposure\"\n\n";
            }

            text += "Make sure you've configured your API key in the plugin settings!";
            return text;
        }
    }
}
