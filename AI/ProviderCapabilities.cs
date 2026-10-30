namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// What each provider can do inside this plugin, stated once. The options panel, the
    /// chat's welcome line and the system-prompt choice all read it from here: before, each
    /// of them kept its own hand-written list of "Anthropic || Google || Ollama", and nothing
    /// told the user which side of that list their provider was on (issue #10).
    /// </summary>
    public static class ProviderCapabilities
    {
        /// <summary>
        /// True when the provider has a tool loop in this plugin: it receives the NINA tool
        /// definitions, acts on tool calls and sees their results. Every working provider has
        /// one since 2.5.7.0; the retired GitHub Models cannot answer at all.
        /// </summary>
        public static bool CanControlEquipment(AIProviderType provider)
        {
            return provider != AIProviderType.GitHub;
        }
    }
}
