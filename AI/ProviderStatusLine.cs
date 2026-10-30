namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// The one-line status shown under the provider selector in the options page. Pure, so
    /// the words a user reads right after choosing a provider are under test.
    /// </summary>
    public static class ProviderStatusLine
    {
        public static string Describe(AIProviderType provider, bool mcpEnabled)
        {
            if (provider == AIProviderType.GitHub)
            {
                return "GitHub Models was retired by GitHub on 30 July 2026 - pick another provider.";
            }

            if (ProviderCapabilities.IsChatOnly(provider))
            {
                return "Chat only - answers questions, cannot read or control NINA equipment (no tool calling in this plugin yet).";
            }

            return mcpEnabled
                ? "✓ Equipment control on - the assistant can read and operate NINA through the Advanced API."
                : "Equipment control available - enable it below to let the assistant read and operate NINA.";
        }
    }
}
