using System;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Utility;

namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// Provider for GitHub Models — RETIRED.
    /// GitHub shut the whole service down for every customer on July 30, 2026
    /// (playground, model catalog, inference API and BYOK). The endpoint this
    /// provider called returns 404 for everyone, so instead of surfacing a raw
    /// HTTP error every request now explains what happened and where to go.
    /// The type is kept so existing configurations still load and get the
    /// explanation rather than a crash.
    /// </summary>
    public class GitHubModelsProvider : IAIProvider
    {
        internal const string RetirementMessage =
            "GitHub retired the GitHub Models service on July 30, 2026 — the free API this provider used " +
            "no longer exists for anyone. Please switch provider in Options → AI Assistant: " +
            "Ollama (runs locally, free, no key), Google Gemini or Mistral (free API tiers).";

        private AIProviderConfig? _config;

        public AIProviderType ProviderType => AIProviderType.GitHub;
        public string DisplayName => "GitHub Models (Retired)";
        public bool IsConfigured => _config != null;
        public bool IsMCPEnabled => false;

        public Task<bool> InitializeAsync(AIProviderConfig config, CancellationToken cancellationToken = default)
        {
            // Initialization still "succeeds" so a stored GitHub configuration keeps
            // loading and every actual use surfaces the retirement explanation.
            _config = config;
            Logger.Warning("GitHub Models provider selected, but the service was retired by GitHub on 2026-07-30");
            return Task.FromResult(true);
        }

        public Task<AIResponse> SendRequestAsync(AIRequest request, CancellationToken cancellationToken = default)
        {
            // No point reaching for the network: the service is gone. Explaining beats a 404.
            Logger.Warning("GitHub Models request refused: the service was retired by GitHub on 2026-07-30");
            return Task.FromResult(new AIResponse { Success = false, Error = RetirementMessage });
        }

        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<string[]> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        {
            // Kept only so a stored model selection still renders in the options UI.
            return Task.FromResult(new[]
            {
                "gpt-4o",
                "gpt-4o-mini",
                "gpt-4.1",
                "gpt-4.1-mini",
                "gpt-5",
                "gpt-5-mini",
                "o1",
                "o1-mini",
                "o3-mini",
                "claude-sonnet-4-5",
                "llama-3.3-70b-instruct",
                "phi-4"
            });
        }
    }
}
