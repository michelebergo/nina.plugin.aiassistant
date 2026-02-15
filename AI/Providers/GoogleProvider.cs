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
    public class GoogleProvider : IAIProvider
    {
        private HttpClient? _httpClient;
        private AIProviderConfig? _config;
        private NINAAdvancedAPIClient? _mcpClient;
        private ExternalMCPClient? _externalMcpClient;
        private MCPConfig? _mcpConfig;
        private bool _mcpEnabled;
        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
        private const int MaxToolIterations = 10; // Prevent infinite loops

        public AIProviderType ProviderType => AIProviderType.Google;
        public string DisplayName => "Google Gemini (MCP Enabled)";
        public bool IsConfigured => _httpClient != null && _config != null;
        public bool IsMCPEnabled => _mcpEnabled && (_mcpClient?.IsConnected == true || _externalMcpClient?.IsConnected == true);

        public async Task<bool> InitializeAsync(AIProviderConfig config, CancellationToken cancellationToken = default)
        {
            try
            {
                _config = config;

                _httpClient = new HttpClient();
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
        /// Set external MCP client for additional tools
        /// </summary>
        public void SetExternalMCP(ExternalMCPClient externalMcpClient)
        {
            _externalMcpClient = externalMcpClient;
            Logger.Info($"External MCP client set for Gemini: {_externalMcpClient.ServerName}");
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
            // Latest: gemini-2.0-flash-001 (stable multimodal), gemini-2.5-pro (most capable)
            var modelId = _config!.ModelId ?? "gemini-2.0-flash-001";
            var systemInstruction = request.SystemPrompt ?? "You are an expert astrophotography assistant for N.I.N.A. (Nighttime Imaging 'N' Astronomy). Only answer astrophotography and astronomy questions. Never fabricate equipment specs or N.I.N.A. features. If unsure, say so.";

            var requestBody = new
            {
                system_instruction = new
                {
                    parts = new[]
                    {
                        new { text = systemInstruction }
                    }
                },
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = request.Prompt }
                        }
                    }
                },
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
                Logger.Error($"Google Gemini API error: {responseContent}");
                return new AIResponse { Success = false, Error = $"API Error: {response.StatusCode} - {responseContent}" };
            }

            return ParseResponse(responseContent, modelId);
        }

        private async Task<AIResponse> SendRequestWithToolsAsync(AIRequest request, CancellationToken cancellationToken)
        {
            // Merge tools from both built-in and external MCP sources
            var allTools = new List<MCPTool>();
            
            if (_mcpClient != null)
            {
                allTools.AddRange(_mcpClient.GetAvailableTools());
                Logger.Info($"GoogleProvider: Added {_mcpClient.GetAvailableTools().Count} built-in NINA API tools");
            }
            
            if (_externalMcpClient != null && _externalMcpClient.IsConnected)
            {
                try
                {
                    var externalTools = await _externalMcpClient.GetToolsAsync(cancellationToken);
                    foreach (var tool in externalTools)
                    {
                        // Convert JObject to MCPTool
                        var properties = tool["inputSchema"]?["properties"]?.ToObject<JObject>();
                        var propDict = new Dictionary<string, MCPToolParameter>();
                        
                        if (properties != null)
                        {
                            foreach (var prop in properties.Properties())
                            {
                                propDict[prop.Name] = new MCPToolParameter
                                {
                                    Type = prop.Value["type"]?.ToString() ?? "string",
                                    Description = prop.Value["description"]?.ToString() ?? ""
                                };
                            }
                        }
                        
                        var mcpTool = new MCPTool
                        {
                            Name = tool["name"]?.ToString() ?? "",
                            Description = tool["description"]?.ToString() ?? "",
                            InputSchema = new MCPToolInputSchema
                            {
                                Properties = propDict,
                                Required = tool["inputSchema"]?["required"]?.ToObject<List<string>>() ?? new List<string>()
                            }
                        };
                        allTools.Add(mcpTool);
                    }
                    Logger.Info($"GoogleProvider: Added {externalTools.Count} external MCP tools");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"GoogleProvider: Failed to get external MCP tools: {ex.Message}");
                }
            }
            
            Logger.Info($"GoogleProvider: Sending request with {allTools.Count} total MCP functions available");
            
            // Convert MCP tools to Gemini function declarations
            var functionDeclarations = allTools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                parameters = new
                {
                    type = "object",
                    properties = t.InputSchema.Properties.ToDictionary(
                        p => p.Key,
                        p => new 
                        { 
                            type = p.Value.Type, 
                            description = p.Value.Description 
                        }
                    ),
                    required = t.InputSchema.Required
                }
            }).ToList();

            var modelId = _config!.ModelId ?? "gemini-2.0-flash-001";
            var systemInstruction = request.SystemPrompt ?? GetMCPSystemPrompt();
            Logger.Debug($"GoogleProvider: Using system prompt: {systemInstruction.Substring(0, Math.Min(100, systemInstruction.Length))}...");
            
            var allToolResults = new List<string>();
            var contents = new List<object>
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = request.Prompt } }
                }
            };

            int iterations = 0;

            while (iterations < MaxToolIterations)
            {
                iterations++;
                Logger.Info($"GoogleProvider: Function calling iteration {iterations}");
                
                var requestBody = new
                {
                    system_instruction = new
                    {
                        parts = new[] { new { text = systemInstruction } }
                    },
                    contents = contents,
                    tools = new[]
                    {
                        new { function_declarations = functionDeclarations }
                    },
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
                    Logger.Error($"Google Gemini API error: {responseContent}");
                    return new AIResponse { Success = false, Error = $"API Error: {response.StatusCode} - {responseContent}" };
                }

                var jsonResponse = JObject.Parse(responseContent);
                var candidates = jsonResponse["candidates"] as JArray;
                
                if (candidates == null || candidates.Count == 0)
                {
                    return new AIResponse { Success = false, Error = "No response from Gemini" };
                }

                var candidate = candidates[0];
                var contentBlock = candidate["content"];
                var parts = contentBlock?["parts"] as JArray;

                if (parts == null || parts.Count == 0)
                {
                    return new AIResponse { Success = false, Error = "Empty response from Gemini" };
                }

                // Add model response to conversation
                contents.Add(new
                {
                    role = "model",
                    parts = parts.ToObject<object>()
                });

                // Check for function calls
                var functionCalls = new List<JObject>();
                string? textResponse = null;

                foreach (var part in parts)
                {
                    if (part["text"] != null)
                    {
                        textResponse = part["text"]?.ToString();
                    }
                    else if (part["functionCall"] != null)
                    {
                        functionCalls.Add((JObject)part["functionCall"]!);
                    }
                }

                // If no function calls, return the text response
                if (functionCalls.Count == 0)
                {
                    var finalResponse = ParseResponse(responseContent, modelId);
                    if (allToolResults.Count > 0)
                    {
                        finalResponse.Metadata ??= new Dictionary<string, object>();
                        finalResponse.Metadata["tool_results"] = allToolResults;
                        finalResponse.Metadata["mcp_enabled"] = true;
                    }
                    return finalResponse;
                }

                // Execute functions and collect results
                var functionResponses = new List<object>();
                foreach (var functionCall in functionCalls)
                {
                    var functionName = functionCall["name"]?.ToString() ?? "";
                    var functionArgs = functionCall["args"]?.ToObject<Dictionary<string, object>>();

                    Logger.Info($"[MCP] Executing function: {functionName}");
                    Logger.Debug($"[MCP] Function arguments: {JsonConvert.SerializeObject(functionArgs)}");
                    
                    // Try built-in first, then external
                    MCPToolResult? result = null;
                    bool isExternal = false;
                    
                    if (_mcpClient != null)
                    {
                        result = await _mcpClient.InvokeToolAsync(functionName, functionArgs, cancellationToken);
                        if (!result.Success && result.Error?.Contains("Unknown tool") == true)
                        {
                            result = null; // Try external
                        }
                    }
                    
                    if (result == null && _externalMcpClient != null && _externalMcpClient.IsConnected)
                    {
                        try
                        {
                            var externalResult = await _externalMcpClient.CallToolAsync(functionName, JObject.FromObject(functionArgs ?? new Dictionary<string, object>()), cancellationToken);
                            result = new MCPToolResult
                            {
                                Success = externalResult["content"] != null,
                                Content = externalResult["content"]?[0]?["text"]?.ToString() ?? externalResult.ToString(),
                                Error = externalResult["error"]?.ToString()
                            };
                            isExternal = true;
                            Logger.Info($"[MCP] Used external MCP server for {functionName}");
                        }
                        catch (Exception ex)
                        {
                            result = new MCPToolResult { Success = false, Error = ex.Message };
                        }
                    }
                    
                    if (result == null)
                    {
                        result = new MCPToolResult { Success = false, Error = "Tool not found in any MCP source" };
                    }
                    
                    Logger.Info($"[MCP] Function {functionName} completed - Success: {result.Success} ({(isExternal ? "External" : "Built-in")})");
                    if (result.Success)
                    {
                        Logger.Debug($"[MCP] Function result: {result.Content?.Substring(0, Math.Min(200, result.Content?.Length ?? 0))}");
                    }
                    else
                    {
                        Logger.Error($"[MCP] Function error: {result.Error}");
                    }
                    
                    var resultContent = result.Success 
                        ? result.Content ?? "Function executed successfully" 
                        : $"Error: {result.Error}";
                    
                    allToolResults.Add($"{functionName}: {(result.Success ? "Success" : "Failed")}");
                    
                    functionResponses.Add(new
                    {
                        functionResponse = new
                        {
                            name = functionName,
                            response = new
                            {
                                name = functionName,
                                content = resultContent
                            }
                        }
                    });
                }

                // Add function results to conversation
                contents.Add(new
                {
                    role = "function",
                    parts = functionResponses
                });
            }

            return new AIResponse
            {
                Success = false,
                Error = "Maximum function calling iterations reached"
            };
        }

        private AIResponse ParseResponse(string responseContent, string modelId)
        {
            var jsonResponse = JObject.Parse(responseContent);
            var messageContent = jsonResponse["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
            var tokensUsed = jsonResponse["usageMetadata"]?["totalTokenCount"]?.Value<int>();

            return new AIResponse
            {
                Success = true,
                Content = messageContent,
                ModelUsed = modelId,
                TokensUsed = tokensUsed,
                Metadata = new Dictionary<string, object>
                {
                    ["provider"] = "Google"
                }
            };
        }

        private string GetMCPSystemPrompt()
        {
            return @"You are an expert astrophotography assistant for N.I.N.A. (Nighttime Imaging 'N' Astronomy) with DIRECT CONTROL over imaging equipment through the NINA Advanced API.

IMPORTANT: You have FUNCTIONS that you MUST USE to interact with NINA. Do NOT just explain how to do things - USE THE FUNCTIONS to actually do them.

Available functions include:
- nina_get_status: Get equipment status (USE THIS when asked about equipment status)
- nina_get_version: Get NINA version
- nina_connect_camera, nina_capture_image: Camera control
- nina_connect_mount, nina_slew_mount, nina_park_mount: Mount control
- nina_connect_focuser, nina_move_focuser: Focuser control
- nina_connect_filterwheel, nina_change_filter: Filter wheel control
- nina_start_guiding, nina_stop_guiding: Guider control

When the user asks to check equipment, get status, or perform ANY action:
1. IMMEDIATELY use the appropriate function - do not just explain
2. Report the actual results from the function
3. Provide helpful interpretation of the data

For example, if user says 'check equipment' or 'show status', USE nina_get_status function first.";
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
                "gemini-2.0-flash-001",
                "gemini-2.5-pro",
                "gemini-2.5-flash",
                "gemini-1.5-flash",
                "gemini-1.5-pro"
            };
        }
    }
}
