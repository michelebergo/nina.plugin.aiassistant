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
    /// Provider for Google Gemini API with MCP tool support
    /// </summary>
    public class GoogleProvider : IMCPCapableProvider
    {
        private HttpClient? _httpClient;
        private AIProviderConfig? _config;
        private NINAAdvancedAPIClient? _mcpClient;
        private IExternalMCPSource? _externalMcpClient;
        private MCPConfig? _mcpConfig;
        private bool _mcpEnabled;
        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
        private const int MaxToolIterations = 10; // Prevent infinite loops

        public AIProviderType ProviderType => AIProviderType.Google;
        public string DisplayName => "Google Gemini (MCP Enabled)";
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
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                Logger.Info("Google Gemini provider initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize Google Gemini provider: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Enable MCP (Model Context Protocol) support for NINA control
        /// </summary>
        public async Task<bool> EnableMCPAsync(MCPConfig mcpConfig, CancellationToken cancellationToken = default)
        {
            try
            {
                _mcpConfig = mcpConfig;
                _mcpClient = new NINAAdvancedAPIClient();
                
                var connected = await _mcpClient.InitializeAsync(mcpConfig, cancellationToken);
                _mcpEnabled = connected && mcpConfig.Enabled;
                
                if (_mcpEnabled)
                {
                    Logger.Info($"MCP enabled for Gemini - Connected to NINA Advanced API at {mcpConfig.NinaHost}:{mcpConfig.NinaPort}");
                }
                else if (mcpConfig.Enabled)
                {
                    Logger.Warning("MCP enabled but could not connect to NINA Advanced API");
                }
                
                return _mcpEnabled;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to enable MCP for Gemini: {ex.Message}");
                _mcpEnabled = false;
                return false;
            }
        }

        /// <summary>
        /// Set external MCP source (single server or multi-server manager) for additional tools
        /// </summary>
        public void SetExternalMCP(IExternalMCPSource externalMcpClient)
        {
            _externalMcpClient = externalMcpClient;
            Logger.Info($"External MCP source set for Gemini: {_externalMcpClient.ServerName}");
        }

        public async Task<AIResponse> SendRequestAsync(AIRequest request, CancellationToken cancellationToken = default)
        {
            if (_httpClient == null || _config == null)
            {
                return new AIResponse { Success = false, Error = "Provider not initialized" };
            }

            try
            {
                Logger.Info($"GoogleProvider: SendRequestAsync - MCP Enabled: {_mcpEnabled}, MCP Client: {(_mcpClient != null ? "Yes" : "No")}, MCP Client Connected: {_mcpClient?.IsConnected}");
                
                // If MCP is enabled, use function-calling flow
                if (_mcpEnabled && _mcpClient != null)
                {
                    Logger.Info("GoogleProvider: Using MCP function-calling flow");
                    return await SendRequestWithToolsAsync(request, cancellationToken);
                }
                
                Logger.Info("GoogleProvider: Using standard request (no MCP)");
                // Standard request without tools
                return await SendStandardRequestAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.Error($"Google Gemini request failed: {ex.Message}");
                return new AIResponse { Success = false, Error = ex.Message };
            }
        }

        private async Task<AIResponse> SendStandardRequestAsync(AIRequest request, CancellationToken cancellationToken)
        {
            // The alias tracks Google's latest stable Flash release; concrete version IDs
            // get retired out from under a hardcoded fallback (gemini-2.0-flash-001 did).
            var modelId = _config!.ModelId ?? "gemini-flash-latest";
            var systemInstruction = SystemPromptComposer.Compose(request.SystemPrompt ?? "You are an expert astrophotography assistant for N.I.N.A. (Nighttime Imaging 'N' Astronomy). Only answer astrophotography and astronomy questions. Never fabricate equipment specs or N.I.N.A. features. If unsure, say so.", request.ProfileContext);

            var contents = new List<object>();
            if (request.History != null)
            {
                foreach (var turn in request.History)
                    contents.Add(new { role = turn.Role == "assistant" ? "model" : "user", parts = new[] { new { text = turn.Content } } });
            }
            contents.Add(new { role = "user", parts = new[] { new { text = request.Prompt } } });

            var requestBody = new
            {
                system_instruction = new
                {
                    parts = new[]
                    {
                        new { text = systemInstruction }
                    }
                },
                contents,
                generationConfig = new
                {
                    temperature = request.Temperature,
                    maxOutputTokens = request.MaxTokens
                }
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{BaseUrl}/{modelId}:generateContent?key={_config.ApiKey}";
            var response = await _httpClient!.PostAsync(url, content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Error($"GoogleProvider: Standard request API error: {response.StatusCode} - {responseContent}");
                return new AIResponse { Success = false, Error = $"API Error: {response.StatusCode} - {responseContent}" };
            }

            return ParseResponse(responseContent, modelId, response.Headers);
        }

        private async Task<AIResponse> SendRequestWithToolsAsync(AIRequest request, CancellationToken cancellationToken)
        {
            var allTools = await MCPToolSources.CollectAsync(_mcpClient, _externalMcpClient, "GoogleProvider", cancellationToken);
            Logger.Info($"GoogleProvider: Sending request with {allTools.Count} total MCP functions available");

            // Convert MCP tools to Gemini function declarations
            var functionDeclarations = allTools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                parameters = GeminiSchema.Parameters(t)
            }).ToList();

            var modelId = _config!.ModelId ?? "gemini-flash-latest";
            var systemInstruction = SystemPromptComposer.Compose(request.SystemPrompt ?? GetMCPSystemPrompt(), request.ProfileContext);
            Logger.Debug($"GoogleProvider: Using system prompt: {systemInstruction.Substring(0, Math.Min(100, systemInstruction.Length))}...");

            var contents = new List<object>();
            if (request.History != null)
            {
                foreach (var turn in request.History)
                    contents.Add(new { role = turn.Role == "assistant" ? "model" : "user", parts = new[] { new { text = turn.Content } } });
            }
            contents.Add(new { role = "user", parts = new[] { new { text = request.Prompt } } });

            System.Net.Http.Headers.HttpResponseHeaders? lastHeaders = null;

            var loop = new ToolLoop(
                new GeminiDialect(),
                post: async (current, ct) =>
                {
                    var requestBody = new
                    {
                        system_instruction = new { parts = new[] { new { text = systemInstruction } } },
                        contents = current,
                        tools = new[] { new { function_declarations = functionDeclarations } },
                        generationConfig = new { temperature = request.Temperature, maxOutputTokens = request.MaxTokens }
                    };
                    var json = JsonConvert.SerializeObject(requestBody);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var url = $"{BaseUrl}/{modelId}:generateContent?key={_config.ApiKey}";
                    var response = await _httpClient!.PostAsync(url, content, ct);
                    lastHeaders = response.Headers;
                    var body = await response.Content.ReadAsStringAsync(ct);
                    if (!response.IsSuccessStatusCode) Logger.Error($"Google Gemini API error: {body}");
                    return (response.StatusCode, body);
                },
                invoke: (name, args, ct) => MCPToolSources.InvokeAsync(_mcpClient, _externalMcpClient, name, args, ct),
                progress: request.ProgressCallback,
                maxIterations: MaxToolIterations);

            var outcome = await loop.RunAsync(contents, cancellationToken);

            if (!outcome.Succeeded)
            {
                return new AIResponse { Success = false, Error = outcome.Error };
            }

            // A 200 with nothing in it: say why (safety block, prompt feedback) rather than
            // hand back an empty answer.
            var final = outcome.FinalResponse!;
            var candidate = (final["candidates"] as JArray)?.FirstOrDefault();
            if (candidate == null)
            {
                return new AIResponse { Success = false, Error = "No response from Gemini" };
            }
            var parts = candidate["content"]?["parts"] as JArray;
            if (parts == null || parts.Count == 0)
            {
                var finishReason = candidate["finishReason"]?.ToString();
                var promptFeedback = final["promptFeedback"]?.ToString();
                var safetyRatings = candidate["safetyRatings"]?.ToString();
                Logger.Error($"GoogleProvider: Gemini returned no content. finishReason={finishReason}, promptFeedback={promptFeedback}, safetyRatings={safetyRatings}, response={final}");
                var detail = finishReason ?? "Unknown";
                if (!string.IsNullOrEmpty(promptFeedback)) detail += $" | Prompt feedback: {promptFeedback}";
                if (!string.IsNullOrEmpty(safetyRatings)) detail += $" | Safety: {safetyRatings}";
                return new AIResponse { Success = false, Error = $"Empty response from Gemini (finishReason: {detail})" };
            }

            var finalResponse = ParseResponse(final.ToString(), modelId, lastHeaders);
            if (outcome.ToolResults.Count > 0)
            {
                finalResponse.Metadata ??= new Dictionary<string, object>();
                finalResponse.Metadata["tool_results"] = outcome.ToolResults;
                finalResponse.Metadata["mcp_enabled"] = true;
            }
            return finalResponse;
        }

        private AIResponse ParseResponse(string responseContent, string modelId, System.Net.Http.Headers.HttpResponseHeaders? headers = null)
        {
            var jsonResponse = JObject.Parse(responseContent);
            var candidates = jsonResponse["candidates"] as JArray;

            if (candidates == null || candidates.Count == 0)
            {
                var promptFeedback = jsonResponse["promptFeedback"]?.ToString();
                Logger.Error($"GoogleProvider: ParseResponse - no candidates. promptFeedback={promptFeedback}, response={responseContent}");
                return new AIResponse { Success = false, Error = $"No response from Gemini (promptFeedback: {promptFeedback ?? "none"})" };
            }

            var candidate = candidates[0];
            var finishReason = candidate["finishReason"]?.ToString();
            var messageContent = candidate["content"]?["parts"]?[0]?["text"]?.ToString();

            if (string.IsNullOrEmpty(messageContent))
            {
                var safetyRatings = candidate["safetyRatings"]?.ToString();
                Logger.Error($"GoogleProvider: ParseResponse - empty text. finishReason={finishReason}, safetyRatings={safetyRatings}, response={responseContent}");
            }

            var usage = jsonResponse["usageMetadata"];
            var promptTokens = usage?["promptTokenCount"]?.Value<int>() ?? 0;
            var candidatesTokens = usage?["candidatesTokenCount"]?.Value<int>() ?? 0;
            var totalTokens = usage?["totalTokenCount"]?.Value<int>() ?? 0;

            var response = new AIResponse
            {
                Success = true,
                Content = messageContent,
                ModelUsed = modelId,
                TokensUsed = totalTokens,
                Metadata = new Dictionary<string, object>
                {
                    ["provider"] = "Google",
                    ["input_tokens"] = promptTokens,
                    ["output_tokens"] = candidatesTokens
                }
            };

            if (headers != null)
            {
                CaptureRateLimits(headers, response);
            }

            return response;
        }

        private void CaptureRateLimits(System.Net.Http.Headers.HttpResponseHeaders headers, AIResponse aiResponse)
        {
            try
            {
                aiResponse.Metadata ??= new Dictionary<string, object>();

                // Gemini headers are not standard, but let's try some common ones
                var limitHeaders = new Dictionary<string, string>
                {
                    ["x-ratelimit-limit-requests"] = "requests_limit",
                    ["x-ratelimit-remaining-requests"] = "requests_remaining",
                    ["x-ratelimit-reset-requests"] = "requests_reset",
                    ["x-ratelimit-limit-tokens"] = "tokens_limit",
                    ["x-ratelimit-remaining-tokens"] = "tokens_remaining",
                    ["x-ratelimit-reset-tokens"] = "tokens_reset",
                    ["retry-after"] = "retry_after"
                };

                foreach (var header in limitHeaders)
                {
                    if (headers.TryGetValues(header.Key, out var values))
                    {
                        aiResponse.Metadata[header.Value] = values.FirstOrDefault() ?? "";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to capture Google rate limit headers: {ex.Message}");
            }
        }

        private string GetMCPSystemPrompt()
        {
            return @"You are an expert astrophotography assistant for N.I.N.A. (Nighttime Imaging 'N' Astronomy) with DIRECT CONTROL over imaging equipment through the NINA Advanced API.

IMPORTANT: You have FUNCTIONS that you MUST USE to interact with NINA. Do NOT just explain how to do things - USE THE FUNCTIONS to actually do them.

Available functions include:
- nina_connect_all_equipment: Connect all equipment at once (USE THIS when asked to 'connect all' instead of calling individual connect tools)
- nina_disconnect_all_equipment: Disconnect all equipment at once (USE THIS when asked to 'disconnect all' instead of calling individual disconnect tools)
- nina_get_status: Get equipment status (USE THIS when asked about equipment status)
- nina_show_profile: The user's own site, telescope, camera and file paths (USE THIS instead of guessing focal length, focal ratio, pixel size, coordinates or where images are written; pass section= to read one part at a time). The profile is the authority on this user's equipment - your general knowledge is not.
- nina_get_version: Get NINA version
- nina_connect_camera, nina_disconnect_camera, nina_capture_image: Camera control
- nina_connect_mount, nina_disconnect_mount, nina_slew_mount, nina_park_mount: Mount control
- nina_connect_focuser, nina_move_focuser: Focuser control
- nina_connect_filterwheel, nina_change_filter: Filter wheel control
- nina_start_guiding, nina_stop_guiding: Guider control

When the user asks to check equipment, get status, or perform ANY action:
1. IMMEDIATELY use the appropriate function - do not just explain
2. Report the actual results from the function
3. Provide helpful interpretation of the data

For example, if user says 'check equipment' or 'show status', USE nina_get_status function first. If user says 'connect all', USE nina_connect_all_equipment.

ADDITIONAL EXTERNAL TOOLS: Besides the NINA equipment functions above, you may ALSO have extra tools from external MCP servers (for example: web page fetching, web search, filesystem access, time/almanac data). They appear in your available tool list, sometimes prefixed with a server name. ALWAYS use whatever tools are actually available to fulfil the user's request - e.g. use a fetch or search tool to look up targets, coordinates, weather, or catalog data online (sites like Telescopius, Stellarium, etc.). Before telling the user you cannot do something, CHECK your available tools and use them. Never claim you lack a capability if a matching tool is present.";
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var testRequest = new AIRequest
                {
                    Prompt = "Hello, confirm you're working.",
                    MaxTokens = 20
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

                var apiKey = _config.ApiKey;
                var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                
                var response = await _httpClient.GetAsync(url, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                    return GetDefaultModels();

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonResponse = JObject.Parse(responseContent);
                var models = jsonResponse["models"]?.ToObject<List<JObject>>();

                if (models == null || models.Count == 0)
                    return GetDefaultModels();

                // Filter to Gemini models that support generateContent
                var modelIds = models
                    .Where(m => 
                    {
                        var name = m["name"]?.ToString();
                        var methods = m["supportedGenerationMethods"]?.ToObject<List<string>>();
                        return !string.IsNullOrEmpty(name) && 
                               name.Contains("gemini") &&
                               methods?.Contains("generateContent") == true;
                    })
                    .Select(m => m["name"]?.ToString()?.Replace("models/", ""))
                    .Where(id => !string.IsNullOrEmpty(id))
                    .OrderByDescending(id => 
                    {
                        if (id!.Contains("2.5")) return 3;
                        if (id.Contains("2.0")) return 2;
                        if (id.Contains("1.5")) return 1;
                        return 0;
                    })
                    .ToArray();

                return modelIds.Length > 0 ? modelIds! : GetDefaultModels();
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to fetch Google models: {ex.Message}");
                return GetDefaultModels();
            }
        }

        private string[] GetDefaultModels()
        {
            return new[]
            {
                // Static fallback, used only when the live model list cannot be fetched.
                // The -latest alias tracks Google's newest stable Flash and never expires;
                // the concrete IDs below were current as of August 2026.
                "gemini-flash-latest",
                "gemini-3.6-flash",
                "gemini-3.5-flash",
                "gemini-3.5-flash-lite",
                "gemini-2.5-flash"
            };
        }
    }
}
