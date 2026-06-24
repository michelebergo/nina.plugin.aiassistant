using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NINA.Core.Utility;

namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// Provider for Mistral AI API (OpenAI-compatible)
    /// </summary>
    public class MistralProvider : IAIProvider
    {
        private HttpClient? _httpClient;
        private AIProviderConfig? _config;
        private const string BaseUrl = "https://api.mistral.ai/v1";

        public AIProviderType ProviderType => AIProviderType.Mistral;
        public string DisplayName => "Mistral AI";
        public bool IsConfigured => _httpClient != null && _config != null;
        public bool IsMCPEnabled => false;

        public async Task<bool> InitializeAsync(AIProviderConfig config, CancellationToken cancellationToken = default)
        {
            try
            {
                _config = config;

                _httpClient = new HttpClient();
                _httpClient.Timeout = TimeSpan.FromMinutes(5);
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", config.ApiKey ?? throw new ArgumentException("API key is required"));
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                Logger.Info("Mistral AI provider initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize Mistral provider: {ex.Message}");
                return false;
            }
        }

        public async Task<AIResponse> SendRequestAsync(AIRequest request, CancellationToken cancellationToken = default)
        {
            if (_httpClient == null || _config == null)
            {
                return new AIResponse { Success = false, Error = "Provider not initialized" };
            }

            try
            {
                var messages = new List<object>
                {
                    new
                    {
                        role = "system",
                        content = request.SystemPrompt ?? "You are an expert astrophotography assistant for N.I.N.A. (Nighttime Imaging 'N' Astronomy). Only answer astrophotography and astronomy questions. Never fabricate equipment specs or N.I.N.A. features. If unsure, say so."
                    },
                    new { role = "user", content = request.Prompt }
                };

                var modelId = _config.ModelId ?? "mistral-large-latest";

                var requestBody = new Dictionary<string, object>
                {
                    ["model"] = modelId,
                    ["messages"] = messages,
                    ["temperature"] = request.Temperature,
                    ["max_tokens"] = request.MaxTokens
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient!.PostAsync($"{BaseUrl}/chat/completions", content, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"Mistral API error: {responseContent}");
                    return new AIResponse { Success = false, Error = $"API Error: {response.StatusCode} - {responseContent}" };
                }

                return ParseResponse(responseContent, modelId);
            }
            catch (Exception ex)
            {
                Logger.Error($"Mistral request failed: {ex.Message}");
                return new AIResponse { Success = false, Error = ex.Message };
            }
        }

        private AIResponse ParseResponse(string responseContent, string modelId)
        {
            var jsonResponse = JObject.Parse(responseContent);
            var choices = jsonResponse["choices"] as JArray;
            var messageContent = choices?[0]?["message"]?["content"]?.ToString();

            var usage = jsonResponse["usage"];
            var promptTokens = usage?["prompt_tokens"]?.Value<int>() ?? 0;
            var completionTokens = usage?["completion_tokens"]?.Value<int>() ?? 0;

            return new AIResponse
            {
                Success = true,
                Content = messageContent,
                ModelUsed = jsonResponse["model"]?.ToString() ?? modelId,
                TokensUsed = promptTokens + completionTokens,
                Metadata = new Dictionary<string, object>
                {
                    ["provider"] = "Mistral",
                    ["input_tokens"] = promptTokens,
                    ["output_tokens"] = completionTokens
                }
            };
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
                if (_httpClient == null || _config == null)
                    return GetDefaultModels();

                var response = await _httpClient.GetAsync($"{BaseUrl}/models", cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warning($"Mistral models API returned {response.StatusCode}, using default list");
                    return GetDefaultModels();
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonResponse = JObject.Parse(responseContent);
                var models = jsonResponse["data"]?.ToObject<List<JObject>>();

                if (models == null || models.Count == 0)
                    return GetDefaultModels();

                // Filter to chat-capable models (exclude embedding models)
                var modelIds = models
                    .Select(m => m["id"]?.ToString())
                    .Where(id => !string.IsNullOrEmpty(id) && !id!.Contains("embed", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(id => id)
                    .ToArray();

                Logger.Info($"Mistral: Found {modelIds.Length} models via API");
                return modelIds.Length > 0 ? modelIds : GetDefaultModels();
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to fetch Mistral models: {ex.Message}, using default list");
                return GetDefaultModels();
            }
        }

        private string[] GetDefaultModels()
        {
            return new[]
            {
                "mistral-large-latest",
                "mistral-medium-latest",
                "mistral-small-latest",
                "open-mistral-7b",
                "open-mixtral-8x7b",
                "codestral-latest"
            };
        }
    }
}