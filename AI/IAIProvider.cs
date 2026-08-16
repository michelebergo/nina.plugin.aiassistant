using System;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// Interface for AI providers that can analyze images and provide intelligent assistance
    /// </summary>
    public interface IAIProvider
    {
        /// <summary>
        /// The type of this AI provider
        /// </summary>
        AIProviderType ProviderType { get; }

        /// <summary>
        /// Display name for the provider
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Whether this provider is currently configured and available
        /// </summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Whether MCP (Model Context Protocol) is enabled and connected for this provider
        /// </summary>
        bool IsMCPEnabled { get; }

        /// <summary>
        /// Initialize the provider with configuration
        /// </summary>
        Task<bool> InitializeAsync(AIProviderConfig config, CancellationToken cancellationToken = default);

        /// <summary>
        /// Point an already-initialized provider at a different model, without rebuilding
        /// anything. The model id travels inside each request payload, so it is request
        /// data rather than connection state: re-initializing for it would throw away a
        /// working HTTP client, and any settings change that arrives while a request or a
        /// model-list fetch is running would race with it. Does nothing when the provider
        /// has not been initialized yet - that case needs a real initialization.
        /// </summary>
        void UpdateModel(string? modelId);

        /// <summary>
        /// Send a request to the AI provider
        /// </summary>
        Task<AIResponse> SendRequestAsync(AIRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Test the connection to verify configuration
        /// </summary>
        Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get information about available models for this provider
        /// </summary>
        Task<string[]> GetAvailableModelsAsync(CancellationToken cancellationToken = default);
    }
}
