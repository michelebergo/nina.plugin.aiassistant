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
        /// definitions, acts on tool calls and sees their results. Without it, asking about
        /// the equipment gets a plain-text "I cannot access NINA" from the model.
        /// </summary>
        public static bool CanControlEquipment(AIProviderType provider)
        {
            return provider == AIProviderType.Anthropic
                || provider == AIProviderType.Google
                || provider == AIProviderType.Ollama;
        }

        /// <summary>
        /// A provider that answers questions but cannot drive NINA. The retired GitHub Models
        /// is deliberately not one of these: it cannot chat either, and its own provider
        /// explains why.
        /// </summary>
        public static bool IsChatOnly(AIProviderType provider)
        {
            return provider == AIProviderType.OpenAI
                || provider == AIProviderType.Mistral;
        }
    }
}
