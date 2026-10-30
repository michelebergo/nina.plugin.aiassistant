namespace NINA.Plugin.AIAssistant.AI {

    /// <summary>
    /// Joins a provider's system prompt to the profile block. It exists because there is no one
    /// place where the system prompt is decided: providers that support MCP build their own,
    /// the chat builds one for the providers that do not, and Anthropic resolves its prompt at
    /// four separate points. Every one of them goes through here, so the rule about how the two
    /// texts meet is written once - and calling it twice on the same text is harmless.
    /// </summary>
    public static class SystemPromptComposer {

        public static string? Compose(string? basePrompt, string? profileBlock) {
            if (string.IsNullOrWhiteSpace(profileBlock)) return basePrompt;

            var block = profileBlock.Trim();
            if (string.IsNullOrWhiteSpace(basePrompt)) return block;
            if (basePrompt.Contains(block)) return basePrompt;

            return basePrompt + "\n\n" + block;
        }
    }
}
