using System.Collections.Generic;

namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// Represents the available AI provider types
    /// </summary>
    public enum AIProviderType
    {
        /// <summary>
        /// GitHub Models - Free tier available via GitHub token
        /// </summary>
        GitHub,

        /// <summary>
        /// OpenAI - GPT-4, GPT-4o, GPT-3.5 (Paid)
        /// </summary>
        OpenAI,

        /// <summary>
        /// Anthropic - Claude 3.5, Claude 3 (Paid)
        /// </summary>
        Anthropic,

        /// <summary>
        /// Google Gemini - Free tier available
        /// </summary>
        Google,

        /// <summary>
        /// Ollama - Local models, completely free
        /// </summary>
        Ollama
    }

    /// <summary>
    /// Model information for display in UI
    /// </summary>
    public class AIModelInfo
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public AIProviderType Provider { get; set; }
        public bool IsFree { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>
    /// Configuration for an AI provider
    /// </summary>
    public class AIProviderConfig
    {
        public AIProviderType Provider { get; set; } = AIProviderType.GitHub;
        public string? ApiKey { get; set; }
        public string? ModelId { get; set; } = "gpt-4o-mini";
        public string? Endpoint { get; set; } // For Ollama or custom endpoints
    }

    /// <summary>
    /// Represents an AI request to analyze image data or make decisions
    /// </summary>
    public class AIRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public string? SystemPrompt { get; set; }
        public byte[]? ImageData { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
        public double Temperature { get; set; } = 0.7;
        public int MaxTokens { get; set; } = 1000;
    }

    /// <summary>
    /// Response from an AI provider
    /// </summary>
    public class AIResponse
    {
        public bool Success { get; set; }
        public string? Content { get; set; }
        public string? Error { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
        public string? ModelUsed { get; set; }
        public int? TokensUsed { get; set; }
    }

    /// <summary>
    /// Static fallback list of models per provider. The Options page fetches live models
    /// from each provider's API via GetAvailableModelsAsync(). These lists are only used
    /// as initial defaults before the API is queried.
    /// </summary>
    public static class AvailableModels
    {
        public static List<AIModelInfo> GetModels(AIProviderType provider)
        {
            return provider switch
            {
                AIProviderType.GitHub => new List<AIModelInfo>
                {
                    new() { Id = "gpt-4o", DisplayName = "GPT-4o", Provider = provider, IsFree = true, Description = "Most capable (default)" },
                    new() { Id = "gpt-4o-mini", DisplayName = "GPT-4o Mini", Provider = provider, IsFree = true, Description = "Fast, affordable" },
                    new() { Id = "o1-mini", DisplayName = "o1 Mini", Provider = provider, IsFree = true, Description = "Reasoning model" },
                    new() { Id = "o1", DisplayName = "o1", Provider = provider, IsFree = true, Description = "Advanced reasoning" },
                    new() { Id = "Llama-3.3-70B-Instruct", DisplayName = "Llama 3.3 70B", Provider = provider, IsFree = true, Description = "Open source" },
                    new() { Id = "Mistral-Large-2411", DisplayName = "Mistral Large", Provider = provider, IsFree = true, Description = "Mistral AI" },
                },
                AIProviderType.OpenAI => new List<AIModelInfo>
                {
                    new() { Id = "gpt-4o", DisplayName = "GPT-4o", Provider = provider, IsFree = false, Description = "Most capable (default)" },
                    new() { Id = "gpt-4o-mini", DisplayName = "GPT-4o Mini", Provider = provider, IsFree = false, Description = "Fast, affordable" },
                    new() { Id = "gpt-4-turbo", DisplayName = "GPT-4 Turbo", Provider = provider, IsFree = false, Description = "128K context" },
                    new() { Id = "gpt-3.5-turbo", DisplayName = "GPT-3.5 Turbo", Provider = provider, IsFree = false, Description = "Legacy, cheap" },
                    new() { Id = "o1-mini", DisplayName = "o1 Mini", Provider = provider, IsFree = false, Description = "Reasoning" },
                    new() { Id = "o1-preview", DisplayName = "o1 Preview", Provider = provider, IsFree = false, Description = "Advanced reasoning" },
                },
                AIProviderType.Anthropic => new List<AIModelInfo>
                {
                    new() { Id = "claude-sonnet-4-5-20250929", DisplayName = "Claude Sonnet 4.5", Provider = provider, IsFree = false, Description = "Latest Sonnet (default)" },
                    new() { Id = "claude-opus-4-6", DisplayName = "Claude Opus 4.6", Provider = provider, IsFree = false, Description = "Most capable, Feb 2026" },
                    new() { Id = "claude-opus-4-5-20251101", DisplayName = "Claude Opus 4.5", Provider = provider, IsFree = false, Description = "Flagship, Nov 2025" },
                    new() { Id = "claude-haiku-4-5-20251001", DisplayName = "Claude Haiku 4.5", Provider = provider, IsFree = false, Description = "Fast, Oct 2025" },
                    new() { Id = "claude-sonnet-4-20250514", DisplayName = "Claude Sonnet 4", Provider = provider, IsFree = false, Description = "May 2025 release" },
                    new() { Id = "claude-3-5-haiku-20241022", DisplayName = "Claude Haiku 3.5", Provider = provider, IsFree = false, Description = "Fast, cheap" },
                },
                AIProviderType.Google => new List<AIModelInfo>
                {
                    new() { Id = "gemini-2.0-flash-001", DisplayName = "Gemini 2.0 Flash", Provider = provider, IsFree = true, Description = "Latest stable (default)" },
                    new() { Id = "gemini-2.5-pro", DisplayName = "Gemini 2.5 Pro", Provider = provider, IsFree = true, Description = "Most capable" },
                    new() { Id = "gemini-1.5-flash", DisplayName = "Gemini 1.5 Flash", Provider = provider, IsFree = true, Description = "Fast, free tier" },
                    new() { Id = "gemini-1.5-pro", DisplayName = "Gemini 1.5 Pro", Provider = provider, IsFree = true, Description = "Previous generation" },
                },
                AIProviderType.Ollama => new List<AIModelInfo>
                {
                    new() { Id = "llama3.2", DisplayName = "Llama 3.2", Provider = provider, IsFree = true, Description = "Local, free" },
                    new() { Id = "mistral", DisplayName = "Mistral 7B", Provider = provider, IsFree = true, Description = "Local, free" },
                    new() { Id = "qwen2.5", DisplayName = "Qwen 2.5", Provider = provider, IsFree = true, Description = "Local, free" },
                    new() { Id = "phi3", DisplayName = "Phi-3", Provider = provider, IsFree = true, Description = "Microsoft, small" },
                    new() { Id = "gemma2", DisplayName = "Gemma 2", Provider = provider, IsFree = true, Description = "Google, local" },
                },
                _ => new List<AIModelInfo>()
            };
        }

        public static List<AIProviderType> GetAllProviders()
        {
            return new List<AIProviderType>
            {
                AIProviderType.GitHub,
                AIProviderType.OpenAI,
                AIProviderType.Anthropic,
                AIProviderType.Google,
                AIProviderType.Ollama
            };
        }

        public static string GetProviderDisplayName(AIProviderType provider)
        {
            return provider switch
            {
                AIProviderType.GitHub => "GitHub Models (Free)",
                AIProviderType.OpenAI => "OpenAI (Paid)",
                AIProviderType.Anthropic => "Anthropic Claude (Paid)",
                AIProviderType.Google => "Google Gemini (Free tier)",
                AIProviderType.Ollama => "Ollama (Local/Free)",
                _ => provider.ToString()
            };
        }
    }
}
