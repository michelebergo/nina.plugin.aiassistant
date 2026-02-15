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

namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// Provider for Anthropic Claude API with MCP tool support
    /// </summary>
    public class AnthropicProvider : IAIProvider
    {
        private HttpClient? _httpClient;
        private AIProviderConfig? _config;
        private NINAAdvancedAPIClient? _mcpClient;
        private MCPConfig? _mcpConfig;
        private bool _mcpEnabled;
        private const string BaseUrl = "https://api.anthropic.com/v1";
        private const string DefaultModel = "claude-sonnet-4-5-20250929";
        private const int MaxToolIterations = 10; // Prevent infinite loops

        public AIProviderType ProviderType => AIProviderType.Anthropic;
        public string DisplayName => "Anthropic Claude (MCP Enabled)";
        public bool IsConfigured => _httpClient != null && _config != null;
        public bool IsMCPEnabled => _mcpEnabled && _mcpClient?.IsConnected == true;

        public async Task<bool> InitializeAsync(AIProviderConfig config, CancellationToken cancellationToken = default)
        {
            try
            {
                _config = config;

                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Add("x-api-key", config.ApiKey ?? throw new ArgumentException("API key is required"));
                _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                Logger.Info("Anthropic provider initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize Anthropic provider: {ex.Message}");
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
                    Logger.Info($"MCP enabled - Connected to NINA Advanced API at {mcpConfig.NinaHost}:{mcpConfig.NinaPort}");
                }
                else if (mcpConfig.Enabled)
                {
                    Logger.Warning("MCP enabled but could not connect to NINA Advanced API");
                }
                
                return _mcpEnabled;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to enable MCP: {ex.Message}");
                _mcpEnabled = false;
                return false;
            }
        }

        /// <summary>
        /// Set external MCP client for additional tools (not implemented for Anthropic yet)
        /// </summary>
        public void SetExternalMCP(NINA.Plugin.AIAssistant.MCP.ExternalMCPClient externalMcpClient)
        {
            Logger.Info("External MCP not yet implemented for Anthropic provider");
            // TODO: Implement external MCP support for Claude similar to Google
        }

        public async Task<AIResponse> SendRequestAsync(AIRequest request, CancellationToken cancellationToken = default)
        {
            if (_httpClient == null || _config == null)
            {
                return new AIResponse { Success = false, Error = "Provider not initialized" };
            }

            try
            {
                Logger.Info($"AnthropicProvider: SendRequestAsync - MCP Enabled: {_mcpEnabled}, MCP Client: {(_mcpClient != null ? "Yes" : "No")}, MCP Client Connected: {_mcpClient?.IsConnected}");
                
                // If MCP is enabled, use tool-calling flow
                if (_mcpEnabled && _mcpClient != null)
                {
                    Logger.Info("AnthropicProvider: Using MCP tool-calling flow");
                    return await SendRequestWithToolsAsync(request, cancellationToken);
                }
                
                Logger.Info("AnthropicProvider: Using standard request (no MCP)");
                // Standard request without tools
                return await SendStandardRequestAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.Error($"Anthropic request failed: {ex.Message}");
                return new AIResponse { Success = false, Error = ex.Message };
            }
        }

        private async Task<AIResponse> SendStandardRequestAsync(AIRequest request, CancellationToken cancellationToken)
        {
            var model = _config!.ModelId ?? DefaultModel;
            var messages = new List<object>
            {
                new { role = "user", content = request.Prompt }
            };

            var requestBody = new
            {
                model,
                max_tokens = request.MaxTokens,
                system = request.SystemPrompt ?? GetDefaultSystemPrompt(),
                messages = messages
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient!.PostAsync($"{BaseUrl}/messages", content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Error($"Anthropic API error: {responseContent}");
                var parsedError = ParseApiError(responseContent);
                
                // If model not found, retry with default model
                if (parsedError.errorType == "not_found_error" && model != DefaultModel)
                {
                    Logger.Warning($"Model '{model}' not found, falling back to '{DefaultModel}'");
                    var fallbackBody = new
                    {
                        model = DefaultModel,
                        max_tokens = request.MaxTokens,
                        system = request.SystemPrompt ?? GetDefaultSystemPrompt(),
                        messages = messages
                    };
                    var fallbackJson = JsonConvert.SerializeObject(fallbackBody);
                    var fallbackContent = new StringContent(fallbackJson, Encoding.UTF8, "application/json");
                    var fallbackResponse = await _httpClient.PostAsync($"{BaseUrl}/messages", fallbackContent, cancellationToken);
                    var fallbackResponseContent = await fallbackResponse.Content.ReadAsStringAsync(cancellationToken);
                    if (fallbackResponse.IsSuccessStatusCode)
                    {
                        var result = ParseResponse(fallbackResponseContent);
                        result.Content = $"⚠️ *Model '{model}' was not found. Used '{DefaultModel}' instead. Please update your model in plugin settings.*\n\n{result.Content}";
                        return result;
                    }
                    return new AIResponse { Success = false, Error = FormatApiError(fallbackResponseContent) };
                }
                
                return new AIResponse { Success = false, Error = FormatApiError(responseContent) };
            }

            return ParseResponse(responseContent);
        }

        private async Task<AIResponse> SendRequestWithToolsAsync(AIRequest request, CancellationToken cancellationToken)
        {
            var tools = _mcpClient!.GetAvailableTools();
            Logger.Info($"AnthropicProvider: Sending request with {tools.Count} MCP tools available");
            
            var toolDefinitions = tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                input_schema = new
                {
                    type = t.InputSchema.Type,
                    properties = t.InputSchema.Properties.ToDictionary(
                        p => p.Key,
                        p => new { type = p.Value.Type, description = p.Value.Description }
                    ),
                    required = t.InputSchema.Required
                }
            }).ToList();

            var messages = new List<object>
            {
                new { role = "user", content = request.Prompt }
            };

            var systemPrompt = request.SystemPrompt ?? GetMCPSystemPrompt();
            Logger.Debug($"AnthropicProvider: Using system prompt: {systemPrompt.Substring(0, Math.Min(100, systemPrompt.Length))}...");
            
            var allToolResults = new List<string>();
            int iterations = 0;
            var model = _config!.ModelId ?? DefaultModel;

            while (iterations < MaxToolIterations)
            {
                iterations++;
                Logger.Info($"AnthropicProvider: Tool iteration {iterations}");
                
                var requestBody = new
                {
                    model,
                    max_tokens = request.MaxTokens,
                    system = systemPrompt,
                    tools = toolDefinitions,
                    messages = messages
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient!.PostAsync($"{BaseUrl}/messages", content, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"Anthropic API error: {responseContent}");
                    var parsedError = ParseApiError(responseContent);
                    
                    // If model not found on first iteration, retry with default model
                    if (parsedError.errorType == "not_found_error" && model != DefaultModel && iterations == 1)
                    {
                        Logger.Warning($"Model '{model}' not found, falling back to '{DefaultModel}' for tool-calling flow");
                        model = DefaultModel;
                        // Rebuild request with default model
                        var retryBody = new
                        {
                            model = DefaultModel,
                            max_tokens = request.MaxTokens,
                            system = systemPrompt,
                            tools = toolDefinitions,
                            messages = messages
                        };
                        json = JsonConvert.SerializeObject(retryBody);
                        content = new StringContent(json, Encoding.UTF8, "application/json");
                        response = await _httpClient.PostAsync($"{BaseUrl}/messages", content, cancellationToken);
                        responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        if (!response.IsSuccessStatusCode)
                        {
                            return new AIResponse { Success = false, Error = FormatApiError(responseContent) };
                        }
                    }
                    else
                    {
                        return new AIResponse { Success = false, Error = FormatApiError(responseContent) };
                    }
                }

                var jsonResponse = JObject.Parse(responseContent);
                var stopReason = jsonResponse["stop_reason"]?.ToString();
                var contentBlocks = jsonResponse["content"] as JArray;

                // Process content blocks
                var assistantContent = new List<object>();
                var toolUseBlocks = new List<JObject>();
                string? textResponse = null;

                if (contentBlocks != null)
                {
                    foreach (var block in contentBlocks)
                    {
                        var blockType = block["type"]?.ToString();
                        
                        if (blockType == "text")
                        {
                            textResponse = block["text"]?.ToString();
                            assistantContent.Add(new { type = "text", text = textResponse });
                        }
                        else if (blockType == "tool_use")
                        {
                            toolUseBlocks.Add((JObject)block);
                            assistantContent.Add(block.ToObject<object>());
                        }
                    }
                }

                // If no tool use, return the response
                if (stopReason != "tool_use" || toolUseBlocks.Count == 0)
                {
                    var finalResponse = ParseResponse(responseContent);
                    if (allToolResults.Count > 0)
                    {
                        finalResponse.Metadata ??= new Dictionary<string, object>();
                        finalResponse.Metadata["tool_results"] = allToolResults;
                        finalResponse.Metadata["mcp_enabled"] = true;
                    }
                    return finalResponse;
                }

                // Add assistant message with tool use
                messages.Add(new { role = "assistant", content = assistantContent });

                // Execute tools and collect results
                var toolResults = new List<object>();
                foreach (var toolUse in toolUseBlocks)
                {
                    var toolId = toolUse["id"]?.ToString() ?? "";
                    var toolName = toolUse["name"]?.ToString() ?? "";
                    var toolInput = toolUse["input"]?.ToObject<Dictionary<string, object>>();

                    Logger.Info($"[MCP] Executing tool: {toolName}");
                    Logger.Debug($"[MCP] Tool ID: {toolId}");
                    Logger.Debug($"[MCP] Tool arguments: {JsonConvert.SerializeObject(toolInput)}");
                    
                    var result = await _mcpClient.InvokeToolAsync(toolName, toolInput, cancellationToken);
                    
                    Logger.Info($"[MCP] Tool {toolName} completed - Success: {result.Success}");
                    if (result.Success)
                    {
                        Logger.Debug($"[MCP] Tool result: {result.Content?.Substring(0, Math.Min(200, result.Content?.Length ?? 0))}");
                    }
                    else
                    {
                        Logger.Error($"[MCP] Tool error: {result.Error}");
                    }
                    
                    var resultContent = result.Success 
                        ? result.Content ?? "Tool executed successfully" 
                        : $"Error: {result.Error}";
                    
                    allToolResults.Add($"{toolName}: {(result.Success ? "Success" : "Failed")}");
                    
                    toolResults.Add(new
                    {
                        type = "tool_result",
                        tool_use_id = toolId,
                        content = resultContent
                    });
                }

                // Add tool results as user message
                messages.Add(new { role = "user", content = toolResults });
            }

            return new AIResponse
            {
                Success = false,
                Error = "Maximum tool iterations reached"
            };
        }

        private AIResponse ParseResponse(string responseContent)
        {
            var jsonResponse = JObject.Parse(responseContent);
            var contentBlocks = jsonResponse["content"] as JArray;
            var textContent = contentBlocks?.FirstOrDefault(b => b["type"]?.ToString() == "text")?["text"]?.ToString();
            var modelUsed = jsonResponse["model"]?.ToString();
            var inputTokens = jsonResponse["usage"]?["input_tokens"]?.Value<int>() ?? 0;
            var outputTokens = jsonResponse["usage"]?["output_tokens"]?.Value<int>() ?? 0;

            return new AIResponse
            {
                Success = true,
                Content = textContent,
                ModelUsed = modelUsed ?? _config?.ModelId,
                TokensUsed = inputTokens + outputTokens,
                Metadata = new Dictionary<string, object>
                {
                    ["provider"] = "Anthropic",
                    ["input_tokens"] = inputTokens,
                    ["output_tokens"] = outputTokens
                }
            };
        }

        private string GetDefaultSystemPrompt()
        {
            return "You are an expert astrophotography assistant for N.I.N.A. (Nighttime Imaging 'N' Astronomy). Only answer astrophotography and astronomy questions. Never fabricate equipment specs or N.I.N.A. features. If unsure, say so.";
        }

        private string GetMCPSystemPrompt()
        {
            return @"You are an expert astrophotography assistant for N.I.N.A. (Nighttime Imaging 'N' Astronomy) with DIRECT CONTROL over imaging equipment through the NINA Advanced API.

IMPORTANT: You have TOOLS that you MUST USE to interact with NINA. Do NOT just explain how to do things - USE THE TOOLS to actually do them.

Available tools include:
- nina_get_status: Get equipment status (USE THIS when asked about equipment status)
- nina_get_version: Get NINA version
- nina_connect_camera, nina_capture_image: Camera control
- nina_connect_mount, nina_slew_mount, nina_park_mount: Mount control
- nina_connect_focuser, nina_move_focuser: Focuser control
- nina_connect_filterwheel, nina_change_filter: Filter wheel control
- nina_start_guiding, nina_stop_guiding: Guider control

When the user asks to check equipment, get status, or perform ANY action:
1. IMMEDIATELY use the appropriate tool - do not just explain
2. Report the actual results from the tool
3. Provide helpful interpretation of the data

For example, if user says 'check equipment' or 'show status', USE nina_get_status tool first.";
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

                // Use Anthropic's /v1/models API to get available models dynamically
                var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/models");
                request.Headers.Add("x-api-key", _config.ApiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");

                var response = await _httpClient.SendAsync(request, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    var json = JObject.Parse(content);
                    var models = json["data"] as JArray;
                    
                    if (models != null && models.Count > 0)
                    {
                        var modelIds = models
                            .Select(m => m["id"]?.ToString())
                            .Where(id => !string.IsNullOrEmpty(id))
                            .ToArray();
                        
                        if (modelIds.Length > 0)
                        {
                            Logger.Info($"Anthropic: Retrieved {modelIds.Length} models from API");
                            return modelIds!;
                        }
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Logger.Warning("Anthropic: Invalid API key");
                    return new[] { DefaultModel };
                }
                
                // Fallback to defaults
                return GetDefaultModels();
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to fetch Anthropic models: {ex.Message}");
                return GetDefaultModels();
            }
        }

        private string[] GetDefaultModels()
        {
            return new[]
            {
                "claude-sonnet-4-5-20250929",
                "claude-sonnet-4-20250514",
                "claude-opus-4-6",
                "claude-opus-4-5-20251101",
                "claude-haiku-4-5-20251001",
                "claude-3-5-haiku-20241022"
            };
        }

        /// <summary>
        /// Parse Anthropic API error response to extract error type and message
        /// </summary>
        private (string errorType, string message) ParseApiError(string responseContent)
        {
            try
            {
                var errorJson = JObject.Parse(responseContent);
                var errorType = errorJson["error"]?["type"]?.ToString() ?? "unknown";
                var message = errorJson["error"]?["message"]?.ToString() ?? responseContent;
                return (errorType, message);
            }
            catch
            {
                return ("unknown", responseContent);
            }
        }

        /// <summary>
        /// Format API error into a user-friendly message
        /// </summary>
        private string FormatApiError(string responseContent)
        {
            var (errorType, message) = ParseApiError(responseContent);
            return errorType switch
            {
                "not_found_error" => $"Model not found: {message}. Please select a valid model in plugin settings.",
                "authentication_error" => $"Invalid API key. Please check your Anthropic API key in plugin settings.",
                "permission_error" => $"API key does not have permission. Check your Anthropic plan and API key.",
                "rate_limit_error" => $"Rate limited by Anthropic. Please wait a moment and try again.",
                "invalid_request_error" => $"Invalid request: {message}",
                "overloaded_error" => $"Anthropic API is overloaded. Please try again shortly.",
                _ => $"API Error ({errorType}): {message}"
            };
        }
    }
}
