using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.Inference;
using NINA.Core.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// Provider for GitHub Models (free tier available)
    /// Uses the Azure AI Inference SDK with GitHub endpoint
    /// </summary>
    public class GitHubModelsProvider : IAIProvider
    {
        private ChatCompletionsClient? _client;
        private AIProviderConfig? _config;
        private HttpClient? _httpClient;

        public AIProviderType ProviderType => AIProviderType.GitHub;
        public string DisplayName => "GitHub Models (Free)";
        public bool IsConfigured => _client != null && _config != null;

        public async Task<bool> InitializeAsync(AIProviderConfig config, CancellationToken cancellationToken = default)
        {
            try
            {
                _config = config;
                _httpClient = new HttpClient();
                _httpClient.Timeout = TimeSpan.FromSeconds(30);

                // GitHub Models endpoint
                var endpoint = new Uri("https://models.inference.ai.azure.com");
                
                // GitHub PAT or API key
                var credential = new AzureKeyCredential(config.ApiKey ?? throw new ArgumentException("API key is required"));

                _client = new ChatCompletionsClient(endpoint, credential);

                Logger.Info("GitHub Models provider initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize GitHub Models provider: {ex.Message}");
                return false;
            }
        }

        public async Task<AIResponse> SendRequestAsync(AIRequest request, CancellationToken cancellationToken = default)
        {
            if (_client == null || _config == null)
            {
                return new AIResponse { Success = false, Error = "Provider not initialized" };
            }

            try
            {
                var systemPrompt = request.SystemPrompt ?? "You are an expert astrophotography assistant for N.I.N.A. (Nighttime Imaging 'N' Astronomy). Only answer astrophotography and astronomy questions. Never fabricate equipment specs or N.I.N.A. features. If unsure, say so.";
                var messages = new ChatRequestMessage[]
                {
                    new ChatRequestSystemMessage(systemPrompt),
                    new ChatRequestUserMessage(request.Prompt)
                };

                var chatOptions = new ChatCompletionsOptions
                {
                    Messages = { messages[0], messages[1] },
                    // GitHub Models doesn't support temperature parameter - uses default (1.0)
                    // Temperature is removed to avoid "unsupported_parameter" errors
                    Model = _config.ModelId ?? "gpt-4o" 
                };
                // GitHub Models uses max_completion_tokens instead of max_tokens
                chatOptions.AdditionalProperties["max_completion_tokens"] = BinaryData.FromObjectAsJson(request.MaxTokens);

                var response = await _client.CompleteAsync(chatOptions, cancellationToken);
                var result = response.Value;
                var firstChoice = result.Content;

                return new AIResponse
                {
                    Success = true,
                    Content = firstChoice,
                    ModelUsed = result.Model ?? _config.ModelId,
                    Metadata = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["provider"] = "GitHub"
                    }
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"GitHub Models request failed: {ex.Message}");
                return new AIResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var testRequest = new AIRequest
                {
                    Prompt = "Hello, confirm you're working.",
                    MaxTokens = 10
                };

                var response = await SendRequestAsync(testRequest, cancellationToken);
                return response.Success;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string[]> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // GitHub Models API endpoint for model listing
                if (_httpClient == null || _config == null)
                    return GetDefaultModels();

                var request = new HttpRequestMessage(HttpMethod.Get, "https://models.inference.ai.azure.com/models");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
                request.Headers.Add("Accept", "application/json");

                var response = await _httpClient.SendAsync(request, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warning($"GitHub Models API returned {response.StatusCode}, using default list");
                    return GetDefaultModels();
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonResponse = JObject.Parse(responseContent);
                var models = jsonResponse["data"]?.ToObject<List<JObject>>();

                if (models == null || models.Count == 0)
                    return GetDefaultModels();

                // Filter to chat-capable models only (exclude embeddings, image, audio, etc.)
                var excludePatterns = new[] { "embed", "whisper", "dall-e", "tts", "jais",
                                              "cohere-command-r", "ai21" };
                
                var modelIds = models
                    .Select(m => m["id"]?.ToString())
                    .Where(id => !string.IsNullOrEmpty(id) &&
                                !excludePatterns.Any(p => id!.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(id => id)
                    .ToArray();

                Logger.Info($"GitHub Models: Found {modelIds.Length} models via API");
                return modelIds.Length > 0 ? modelIds : GetDefaultModels();
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to fetch GitHub models: {ex.Message}, using default list");
                return GetDefaultModels();
            }
        }

        private string[] GetDefaultModels()
        {
            // GitHub Models available as of January 2026 (fallback)
            return new[]
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
            };
        }
    }
}
