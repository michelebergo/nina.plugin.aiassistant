using System;
using System.Collections.Generic;
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
    /// Provider for Ollama (local models, completely free)
    /// </summary>
    public class OllamaProvider : IAIProvider
    {
        private HttpClient? _httpClient;
        private AIProviderConfig? _config;

        public AIProviderType ProviderType => AIProviderType.Ollama;
        public string DisplayName => "Ollama (Local/Free)";
        public bool IsConfigured => _httpClient != null && _config != null;

        public async Task<bool> InitializeAsync(AIProviderConfig config, CancellationToken cancellationToken = default)
        {
            try
            {
                _config = config;

                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.Timeout = TimeSpan.FromMinutes(5); // Local models can be slow on first load

                Logger.Info("Ollama provider initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize Ollama provider: {ex.Message}");
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
                var endpoint = _config.Endpoint ?? "http://localhost:11434";
                var modelId = _config.ModelId ?? "llama3.2";
                var systemPrompt = request.SystemPrompt ?? "You are an expert astrophotography assistant for N.I.N.A. (Nighttime Imaging 'N' Astronomy). Only answer astrophotography and astronomy questions. Never fabricate equipment specs or N.I.N.A. features. If unsure, say so.";

                var requestBody = new
                {
                    model = modelId,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = request.Prompt }
                    },
                    stream = false,
                    options = new
                    {
                        temperature = request.Temperature,
                        num_predict = request.MaxTokens
                    }
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{endpoint}/api/chat", content, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"Ollama API error: {responseContent}");
                    return new AIResponse { Success = false, Error = $"API Error: {response.StatusCode} - {responseContent}" };
                }

                var jsonResponse = JObject.Parse(responseContent);
                var messageContent = jsonResponse["message"]?["content"]?.ToString();
                var evalCount = jsonResponse["eval_count"]?.Value<int>();

                return new AIResponse
                {
                    Success = true,
                    Content = messageContent,
                    ModelUsed = modelId,
                    TokensUsed = evalCount,
                    Metadata = new Dictionary<string, object>
                    {
                        ["provider"] = "Ollama",
                        ["local"] = true
                    }
                };
            }
            catch (HttpRequestException ex)
            {
                Logger.Error($"Ollama connection failed: {ex.Message}");
                return new AIResponse 
                { 
                    Success = false, 
                    Error = $"Cannot connect to Ollama. Make sure Ollama is running at {_config?.Endpoint ?? "http://localhost:11434"}" 
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"Ollama request failed: {ex.Message}");
                return new AIResponse { Success = false, Error = ex.Message };
            }
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_httpClient == null || _config == null)
                    return false;

                var endpoint = _config.Endpoint ?? "http://localhost:11434";
                
                // First check if Ollama is running
                var response = await _httpClient.GetAsync($"{endpoint}/api/tags", cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return false;

                // Then try a simple chat
                var testRequest = new AIRequest
                {
                    Prompt = "Hello",
                    MaxTokens = 10
                };

                var chatResponse = await SendRequestAsync(testRequest, cancellationToken);
                return chatResponse.Success;
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

                var endpoint = _config.Endpoint ?? "http://localhost:11434";
                var response = await _httpClient.GetAsync($"{endpoint}/api/tags", cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                    return GetDefaultModels();

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonResponse = JObject.Parse(responseContent);
                var models = jsonResponse["models"]?.ToObject<List<JObject>>();

                if (models == null || models.Count == 0)
                    return GetDefaultModels();

                var modelNames = new List<string>();
                foreach (var model in models)
                {
                    var name = model["name"]?.ToString();
                    if (!string.IsNullOrEmpty(name))
                        modelNames.Add(name);
                }

                return modelNames.Count > 0 ? modelNames.ToArray() : GetDefaultModels();
            }
            catch
            {
                return GetDefaultModels();
            }
        }

        private string[] GetDefaultModels()
        {
            return new[]
            {
                "llama3.2",
                "mistral",
                "qwen2.5",
                "phi3",
                "gemma2"
            };
        }
    }
}
