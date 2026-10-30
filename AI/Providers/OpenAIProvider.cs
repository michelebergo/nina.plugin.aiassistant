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
using NINA.Plugin.AIAssistant.AI.MCP;
using NINA.Plugin.AIAssistant.MCP;

namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// Provider for OpenAI API (GPT-4, GPT-4o, etc.) with MCP tool support
    /// </summary>
    public class OpenAIProvider : IMCPCapableProvider
    {
        private HttpClient? _httpClient;
        private AIProviderConfig? _config;
        private NINAAdvancedAPIClient? _mcpClient;
        private IExternalMCPSource? _externalMcpClient;
        private bool _mcpEnabled;
        private const string BaseUrl = "https://api.openai.com/v1";
        private const int MaxToolIterations = 10;
        private const int MaxTools = 128;

        public AIProviderType ProviderType => AIProviderType.OpenAI;
        public string DisplayName => "OpenAI (MCP Enabled)";
        public bool IsConfigured => _httpClient != null && _config != null;
        public bool IsMCPEnabled => _mcpEnabled && (_mcpClient?.IsConnected == true || _externalMcpClient?.IsConnected == true);

        public void UpdateModel(string? modelId)
        {
            if (_config != null) { _config.ModelId = modelId; }
        }

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

                Logger.Info("OpenAI provider initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize OpenAI provider: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> EnableMCPAsync(MCPConfig mcpConfig, CancellationToken cancellationToken = default)
        {
            try
            {
                _mcpClient = new NINAAdvancedAPIClient();
                var connected = await _mcpClient.InitializeAsync(mcpConfig, cancellationToken);
                _mcpEnabled = connected && mcpConfig.Enabled;

                if (_mcpEnabled)
                {
                    Logger.Info($"MCP enabled for OpenAI - Connected to NINA Advanced API at {mcpConfig.NinaHost}:{mcpConfig.NinaPort}");
                }
                else if (mcpConfig.Enabled)
                {
                    Logger.Warning("MCP enabled but could not connect to NINA Advanced API");
                }
                return _mcpEnabled;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to enable MCP for OpenAI: {ex.Message}");
                _mcpEnabled = false;
                return false;
            }
        }

        public void SetExternalMCP(IExternalMCPSource externalMcpClient)
        {
            _externalMcpClient = externalMcpClient;
            Logger.Info($"External MCP source set for OpenAI: {_externalMcpClient.ServerName}");
        }

        public async Task<AIResponse> SendRequestAsync(AIRequest request, CancellationToken cancellationToken = default)
        {
            if (_httpClient == null || _config == null)
            {
                return new AIResponse { Success = false, Error = "Provider not initialized" };
            }

            try
            {
                if (_mcpEnabled && _mcpClient != null)
                {
                    Logger.Info("OpenAIProvider: Using MCP tool-calling flow");
                    return await SendRequestWithToolsAsync(request, cancellationToken);
                }

                var messages = new List<object>
                {
                    new {
                        role = "system",
                        content = SystemPromptComposer.Compose(request.SystemPrompt ?? "You are an expert astrophotography assistant for N.I.N.A. (Nighttime Imaging 'N' Astronomy). Only answer astrophotography and astronomy questions. Never fabricate equipment specs or N.I.N.A. features. If unsure, say so.", request.ProfileContext)
                    }
                };
                if (request.History != null)
                {
                    foreach (var turn in request.History)
                        messages.Add(new { role = turn.Role, content = turn.Content });
                }
                messages.Add(new { role = "user", content = request.Prompt });

                var modelId = _config.ModelId ?? "gpt-4o";
                var requestBody = BuildBody(modelId, messages, request);
                var (response, responseContent) = await PostWithFallbacksAsync(requestBody, modelId, request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"OpenAI API error: {responseContent}");
                    var errorResp = new AIResponse { Success = false, Error = $"API Error: {response.StatusCode} - {responseContent}" };
                    CaptureRateLimits(response.Headers, errorResp.Metadata ??= new Dictionary<string, object>());
                    return errorResp;
                }

                var aiResponse = ParseResponse(JObject.Parse(responseContent), modelId);
                CaptureRateLimits(response.Headers, aiResponse.Metadata!);
                return aiResponse;
            }
            catch (Exception ex)
            {
                Logger.Error($"OpenAI request failed: {ex.Message}");
                return new AIResponse { Success = false, Error = ex.Message };
            }
        }

        private async Task<AIResponse> SendRequestWithToolsAsync(AIRequest request, CancellationToken cancellationToken)
        {
            var modelId = _config!.ModelId ?? "gpt-4o";
            var systemPrompt = SystemPromptComposer.Compose(request.SystemPrompt ?? MCPSystemPrompts.Default, request.ProfileContext);

            var allTools = await MCPToolSources.CollectAsync(_mcpClient, _externalMcpClient, "OpenAIProvider", cancellationToken);
            // OpenAI refuses more than 128 tools in one request ("array too long"); the bridge
            // declares more than that, so the least conversational ones are left out.
            var fitted = ToolCap.Apply(allTools, MaxTools, out var dropped);
            if (dropped.Count > 0)
            {
                Logger.Info($"OpenAIProvider: {allTools.Count} tools available, {dropped.Count} left out to fit OpenAI's limit of {MaxTools}: {string.Join(", ", dropped)}");
            }
            var tools = ChatCompletionsTools.BuildTools(fitted);
            Logger.Info($"OpenAIProvider: Sending request with {fitted.Count} MCP tools");

            var messages = new List<object> { new { role = "system", content = systemPrompt } };
            if (request.History != null)
            {
                foreach (var turn in request.History)
                    messages.Add(new { role = turn.Role, content = turn.Content });
            }
            messages.Add(new { role = "user", content = request.Prompt });

            // Headers of the last answer, so the quota readout reflects the request that
            // actually produced the text and not the first of several.
            HttpResponseHeaders? lastHeaders = null;

            var loop = new ChatCompletionsToolLoop(
                post: async (current, ct) =>
                {
                    var body = BuildBody(modelId, current.ToList(), request);
                    body["tools"] = tools;
                    var (response, content) = await PostWithFallbacksAsync(body, modelId, request, ct);
                    lastHeaders = response.Headers;
                    if ((int)response.StatusCode == 400)
                    {
                        var explanation = OpenAIRequestNegotiation.ExplainUnrepairable(body, content);
                        if (explanation != null) content = explanation;
                    }
                    return (response.StatusCode, content);
                },
                invoke: (name, args, ct) => MCPToolSources.InvokeAsync(_mcpClient, _externalMcpClient, name, args, ct),
                progress: request.ProgressCallback,
                includeToolNameInResult: false,
                maxIterations: MaxToolIterations);

            var outcome = await loop.RunAsync(messages, cancellationToken);

            if (!outcome.Succeeded)
            {
                var errorResp = new AIResponse { Success = false, Error = outcome.Error, Metadata = new Dictionary<string, object>() };
                if (lastHeaders != null) CaptureRateLimits(lastHeaders, errorResp.Metadata);
                return errorResp;
            }

            var aiResponse = ParseResponse(outcome.FinalResponse!, modelId);
            if (outcome.ToolResults.Count > 0)
            {
                aiResponse.Metadata!["tool_results"] = outcome.ToolResults;
                aiResponse.Metadata["mcp_enabled"] = true;
            }
            if (lastHeaders != null) CaptureRateLimits(lastHeaders, aiResponse.Metadata!);
            return aiResponse;
        }

        /// <summary>
        /// Reasoning models (o1, o3, o4) and the newer families (gpt-4.1+, gpt-5+) take
        /// max_completion_tokens instead of max_tokens and refuse a custom temperature.
        /// </summary>
        private static Dictionary<string, object> BuildBody(string modelId, List<object> messages, AIRequest request)
        {
            var isReasoningModel = modelId.StartsWith("o1") || modelId.StartsWith("o3") ||
                                   modelId.StartsWith("o4") || modelId.Contains("gpt-4.1") ||
                                   modelId.Contains("gpt-5");

            var body = new Dictionary<string, object>
            {
                ["model"] = modelId,
                ["messages"] = messages
            };
            if (!isReasoningModel)
            {
                body["temperature"] = request.Temperature;
            }
            if (isReasoningModel)
            {
                body["max_completion_tokens"] = request.MaxTokens;
            }
            else
            {
                body["max_tokens"] = request.MaxTokens;
            }
            return body;
        }

        /// <summary>
        /// Posts the body and, when the model refuses a parameter, repairs the one it names and
        /// tries again - a few times at most, since each repair is applied once. Shared by the
        /// plain chat path and the tool loop; the repairs that worked are kept for the next
        /// request of the same conversation so the first attempt stops failing.
        /// </summary>
        private async Task<(HttpResponseMessage Response, string Content)> PostWithFallbacksAsync(
            Dictionary<string, object> requestBody, string modelId, AIRequest request, CancellationToken cancellationToken)
        {
            // What one model taught us does not hold for the next: gpt-4.1 rejects the
            // reasoning_effort that gpt-6-astra demanded.
            if (modelId != _learnedForModel) { _learnedParameters.Clear(); _learnedForModel = modelId; }

            foreach (var (key, value) in _learnedParameters)
            {
                if (value == null) requestBody.Remove(key); else requestBody[key] = value;
            }

            var (response, responseContent) = await PostAsync(requestBody, cancellationToken);

            for (int attempt = 0; attempt < 3 && (int)response.StatusCode == 400; attempt++)
            {
                var before = new Dictionary<string, object>(requestBody);
                if (!OpenAIRequestNegotiation.Adjust(requestBody, request.MaxTokens, responseContent)) break;

                Logger.Warning($"OpenAI model '{modelId}' refused a parameter, retrying with the request adjusted: {Summarize(responseContent)}");
                RememberAdjustment(before, requestBody);
                (response, responseContent) = await PostAsync(requestBody, cancellationToken);
            }

            return (response, responseContent);
        }

        // Parameter repairs the model has taught us (null = removed), replayed on later
        // requests. Reset when the model changes, since the next one may want the opposite.
        private readonly Dictionary<string, object?> _learnedParameters = new();
        private string? _learnedForModel;

        private void RememberAdjustment(Dictionary<string, object> before, Dictionary<string, object> after)
        {
            foreach (var key in new[] { "temperature", "max_tokens", "max_completion_tokens", "reasoning_effort" })
            {
                var had = before.ContainsKey(key);
                var has = after.ContainsKey(key);
                if (had && !has) _learnedParameters[key] = null;
                else if (has && (!had || !Equals(before[key], after[key]))) _learnedParameters[key] = after[key];
            }
        }

        private static string Summarize(string errorBody)
        {
            try
            {
                return JObject.Parse(errorBody)["error"]?["message"]?.ToString() ?? errorBody;
            }
            catch
            {
                return errorBody;
            }
        }

        private async Task<(HttpResponseMessage, string)> PostAsync(Dictionary<string, object> requestBody, CancellationToken cancellationToken)
        {
            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient!.PostAsync($"{BaseUrl}/chat/completions", content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return (response, responseContent);
        }

        private AIResponse ParseResponse(JObject jsonResponse, string modelId)
        {
            var messageContent = jsonResponse["choices"]?[0]?["message"]?["content"]?.ToString();
            var usage = jsonResponse["usage"];
            var promptTokens = usage?["prompt_tokens"]?.Value<int>();
            var completionTokens = usage?["completion_tokens"]?.Value<int>();
            var tokensUsed = usage?["total_tokens"]?.Value<int>();

            var metadata = new Dictionary<string, object> { ["provider"] = "OpenAI" };
            if (promptTokens.HasValue && completionTokens.HasValue)
            {
                metadata["input_tokens"] = promptTokens.Value;
                metadata["output_tokens"] = completionTokens.Value;
            }

            return new AIResponse
            {
                Success = true,
                Content = messageContent,
                ModelUsed = jsonResponse["model"]?.ToString() ?? modelId,
                TokensUsed = tokensUsed,
                Metadata = metadata
            };
        }

        private static void CaptureRateLimits(HttpResponseHeaders headers, Dictionary<string, object> metadata)
        {
            try
            {
                var limitHeaders = new Dictionary<string, string>
                {
                    ["x-ratelimit-limit-requests"] = "requests_limit",
                    ["x-ratelimit-remaining-requests"] = "requests_remaining",
                    ["x-ratelimit-reset-requests"] = "requests_reset",
                    ["x-ratelimit-limit-tokens"] = "tokens_limit",
                    ["x-ratelimit-remaining-tokens"] = "tokens_remaining",
                    ["x-ratelimit-reset-tokens"] = "tokens_reset"
                };

                foreach (var header in limitHeaders)
                {
                    if (headers.TryGetValues(header.Key, out var values))
                    {
                        metadata[header.Value] = values.FirstOrDefault() ?? "";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to capture OpenAI rate limit headers: {ex.Message}");
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
                if (_httpClient == null || _config == null)
                    return GetDefaultModels();

                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return GetDefaultModels();

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonResponse = JObject.Parse(responseContent);
                var models = jsonResponse["data"]?.ToObject<List<JObject>>();

                if (models == null || models.Count == 0)
                    return GetDefaultModels();

                // Filter to only chat/completion models
                // Exclude: embeddings, tts, whisper, dall-e, moderation, babbage, davinci, realtime, search
                var excludePatterns = new[] { "embed", "tts", "whisper", "dall-e", "moderation",
                                              "babbage", "davinci", "realtime", "search", "audio",
                                              "transcription", "computer" };

                var modelIds = models
                    .Select(m => m["id"]?.ToString())
                    .Where(id => !string.IsNullOrEmpty(id) &&
                                (id!.StartsWith("gpt") || id.StartsWith("o1") || id.StartsWith("o3") || id.StartsWith("o4")) &&
                                !excludePatterns.Any(p => id.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(id => id) // Latest versions first
                    .ToArray();

                return modelIds.Length > 0 ? modelIds! : GetDefaultModels();
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to fetch OpenAI models: {ex.Message}");
                return GetDefaultModels();
            }
        }

        private string[] GetDefaultModels()
        {
            return new[]
            {
                "gpt-4o",
                "gpt-4o-mini",
                "gpt-4-turbo",
                "gpt-3.5-turbo",
                "o1-mini",
                "o1-preview"
            };
        }
    }
}
