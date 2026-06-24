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
    /// Provider for Ollama (local models, completely free) with MCP tool support
    /// </summary>
    public class OllamaProvider : IAIProvider
    {
        private HttpClient? _httpClient;
        private AIProviderConfig? _config;
        private NINAAdvancedAPIClient? _mcpClient;
        private ExternalMCPClient? _externalMcpClient;
        private MCPConfig? _mcpConfig;
        private bool _mcpEnabled;
        private const int MaxToolIterations = 10; // Prevent infinite loops

        public AIProviderType ProviderType => AIProviderType.Ollama;
        public string DisplayName => "Ollama (Local/Free, MCP Enabled)";
        public bool IsConfigured => _httpClient != null && _config != null;
        public bool IsMCPEnabled => _mcpEnabled && (_mcpClient?.IsConnected == true || _externalMcpClient?.IsConnected == true);

        public async Task<bool> InitializeAsync(AIProviderConfig config, CancellationToken cancellationToken = default)
        {
            try
            {
                _config = config;

                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.Timeout = TimeSpan.FromMinutes(10); // Local models can be slow on first load + tool calls

                Logger.Info("Ollama provider initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize Ollama provider: {ex.Message}");
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
                    Logger.Info($"MCP enabled for Ollama - Connected to NINA Advanced API at {mcpConfig.NinaHost}:{mcpConfig.NinaPort}");
                }
                else if (mcpConfig.Enabled)
                {
                    Logger.Warning("MCP enabled but could not connect to NINA Advanced API");
                }
                
                return _mcpEnabled;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to enable MCP for Ollama: {ex.Message}");
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
            Logger.Info($"External MCP client set for Ollama: {_externalMcpClient.ServerName}");
        }

        public async Task<AIResponse> SendRequestAsync(AIRequest request, CancellationToken cancellationToken = default)
        {
            if (_httpClient == null || _config == null)
            {
                return new AIResponse { Success = false, Error = "Provider not initialized" };
            }

            try
            {
                Logger.Info($"OllamaProvider: SendRequestAsync - MCP Enabled: {_mcpEnabled}, MCP Client: {(_mcpClient != null ? "Yes" : "No")}, MCP Client Connected: {_mcpClient?.IsConnected}");
                
                // If MCP is enabled, use tool-calling flow
                if (_mcpEnabled && _mcpClient != null)
                {
                    Logger.Info("OllamaProvider: Using MCP tool-calling flow");
                    return await SendRequestWithToolsAsync(request, cancellationToken);
                }
                
                Logger.Info("OllamaProvider: Using standard request (no MCP)");
                // Standard request without tools
                return await SendStandardRequestAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.Error($"Ollama request failed: {ex.Message}");
                return new AIResponse { Success = false, Error = ex.Message };
            }
        }

        private async Task<AIResponse> SendStandardRequestAsync(AIRequest request, CancellationToken cancellationToken)
        {
            var endpoint = _config!.Endpoint ?? "http://localhost:11434";
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

            var response = await _httpClient!.PostAsync($"{endpoint}/api/chat", content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Error($"Ollama API error: {responseContent}");
                return new AIResponse { Success = false, Error = $"API Error: {response.StatusCode} - {responseContent}" };
            }

            return ParseResponse(responseContent, modelId);
        }

        /// <summary>
        /// Send request with MCP tool-calling support using Ollama's native tool API
        /// </summary>
        private async Task<AIResponse> SendRequestWithToolsAsync(AIRequest request, CancellationToken cancellationToken)
        {
            var endpoint = _config!.Endpoint ?? "http://localhost:11434";
            var modelId = _config.ModelId ?? "llama3.2";
            var systemPrompt = request.SystemPrompt ?? GetMCPSystemPrompt();

            // Merge tools from both built-in and external MCP sources
            var allTools = new List<MCPTool>();
            
            if (_mcpClient != null)
            {
                allTools.AddRange(_mcpClient.GetAvailableTools());
                Logger.Info($"OllamaProvider: Added {_mcpClient.GetAvailableTools().Count} built-in NINA API tools");
            }
            
            if (_externalMcpClient != null && _externalMcpClient.IsConnected)
            {
                try
                {
                    var externalTools = await _externalMcpClient.GetToolsAsync(cancellationToken);
                    foreach (var tool in externalTools)
                    {
                        var toolName = tool["name"]?.ToString();
                        if (string.IsNullOrEmpty(toolName)) continue;
                        
                        // Check for name collision with NINA built-in tools
                        if (allTools.Any(t => t.Name == toolName))
                        {
                            Logger.Warning($"OllamaProvider: Skipping external MCP tool '{toolName}' because a tool with this name already exists");
                            continue;
                        }

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
                    Logger.Info($"OllamaProvider: Added {externalTools.Count} external MCP tools");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"OllamaProvider: Failed to get external MCP tools: {ex.Message}");
                }
            }

            Logger.Info($"OllamaProvider: Sending request with {allTools.Count} total MCP tools available");

            // Convert MCP tools to Ollama tool format
            var ollamaTools = allTools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = new
                    {
                        type = t.InputSchema.Type,
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
                }
            }).ToList();

            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = request.Prompt }
            };

            var allToolResults = new List<string>();
            int iterations = 0;

            while (iterations < MaxToolIterations)
            {
                iterations++;
                Logger.Info($"OllamaProvider: Tool iteration {iterations}");

                var requestBody = new
                {
                    model = modelId,
                    messages = messages,
                    tools = ollamaTools,
                    stream = false,
                    options = new
                    {
                        temperature = request.Temperature,
                        num_predict = request.MaxTokens
                    }
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient!.PostAsync($"{endpoint}/api/chat", content, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"Ollama API error: {responseContent}");
                    return new AIResponse { Success = false, Error = $"API Error: {response.StatusCode} - {responseContent}" };
                }

                var jsonResponse = JObject.Parse(responseContent);
                var message = jsonResponse["message"];
                var toolCalls = message?["tool_calls"] as JArray;
                var assistantContent = message?["content"]?.ToString();

                // If no tool calls, return the final response
                if (toolCalls == null || toolCalls.Count == 0)
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

                // Add assistant message (with tool calls) to conversation
                messages.Add(new
                {
                    role = "assistant",
                    content = assistantContent ?? "",
                    tool_calls = toolCalls.ToObject<object>()
                });

                // Execute tools and collect results
                var toolTasks = toolCalls.Select(async toolCall =>
                {
                    var function = toolCall["function"];
                    var toolName = function?["name"]?.ToString() ?? "";
                    var toolArgsJson = function?["arguments"]?.ToString() ?? "{}";
                    var toolInput = JsonConvert.DeserializeObject<Dictionary<string, object>>(toolArgsJson) ?? new Dictionary<string, object>();

                    Logger.Info($"[MCP] Executing tool: {toolName}");
                    Logger.Debug($"[MCP] Tool arguments: {toolArgsJson}");

                    // Try built-in first, then external
                    MCPToolResult? result = null;
                    bool isExternal = false;

                    if (_mcpClient != null)
                    {
                        result = await _mcpClient.InvokeToolAsync(toolName, toolInput, cancellationToken);
                        if (!result.Success && result.Error?.Contains("Unknown tool") == true)
                        {
                            result = null; // Try external
                        }
                    }

                    if (result == null && _externalMcpClient != null && _externalMcpClient.IsConnected)
                    {
                        try
                        {
                            var externalResult = await _externalMcpClient.CallToolAsync(toolName, JObject.FromObject(toolInput), cancellationToken);
                            result = new MCPToolResult
                            {
                                Success = externalResult["content"] != null,
                                Content = externalResult["content"]?[0]?["text"]?.ToString() ?? externalResult.ToString(),
                                Error = externalResult["error"]?.ToString()
                            };
                            isExternal = true;
                            Logger.Info($"[MCP] Used external MCP server for {toolName}");
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

                    Logger.Info($"[MCP] Tool {toolName} completed - Success: {result.Success} ({(isExternal ? "External" : "Built-in")})");

                    var resultContent = result.Success
                        ? result.Content ?? "Tool executed successfully"
                        : $"Error: {result.Error}";

                    return new
                    {
                        ToolName = toolName,
                        Success = result.Success,
                        ResultObj = new
                        {
                            role = "tool",
                            content = resultContent
                        }
                    };
                }).ToList();

                var completedTasks = await Task.WhenAll(toolTasks);

                foreach (var t in completedTasks)
                {
                    allToolResults.Add($"{t.ToolName}: {(t.Success ? "Success" : "Failed")}");
                    messages.Add(t.ResultObj);
                }
            }

            return new AIResponse
            {
                Success = false,
                Error = "Maximum tool iterations reached"
            };
        }

        private AIResponse ParseResponse(string responseContent, string modelId)
        {
            var jsonResponse = JObject.Parse(responseContent);
            var messageContent = jsonResponse["message"]?["content"]?.ToString();

            var promptTokens = jsonResponse["prompt_eval_count"]?.Value<int>() ?? 0;
            var evalTokens = jsonResponse["eval_count"]?.Value<int>() ?? 0;

            return new AIResponse
            {
                Success = true,
                Content = messageContent,
                ModelUsed = modelId,
                TokensUsed = promptTokens + evalTokens,
                Metadata = new Dictionary<string, object>
                {
                    ["provider"] = "Ollama",
                    ["local"] = true,
                    ["input_tokens"] = promptTokens,
                    ["output_tokens"] = evalTokens
                }
            };
        }

        private string GetMCPSystemPrompt()
        {
            return @"You are an expert astrophotography assistant for N.I.N.A. (Nighttime Imaging 'N' Astronomy) with DIRECT CONTROL over imaging equipment through the NINA Advanced API.

IMPORTANT: You have TOOLS that you MUST USE to interact with NINA. Do NOT just explain how to do things - USE THE TOOLS to actually do them.

Available tools include:
- nina_connect_all_equipment: Connect all equipment at once (USE THIS when asked to 'connect all' instead of calling individual connect tools)
- nina_disconnect_all_equipment: Disconnect all equipment at once (USE THIS when asked to 'disconnect all' instead of calling individual disconnect tools)
- nina_get_status: Get equipment status (USE THIS when asked about equipment status)
- nina_get_version: Get NINA version
- nina_connect_camera, nina_disconnect_camera, nina_capture_image: Camera control
- nina_connect_mount, nina_disconnect_mount, nina_slew_mount, nina_park_mount: Mount control
- nina_connect_focuser, nina_move_focuser: Focuser control
- nina_connect_filterwheel, nina_change_filter: Filter wheel control
- nina_start_guiding, nina_stop_guiding: Guider control

When the user asks to check equipment, get status, or perform ANY action:
1. IMMEDIATELY use the appropriate tool - do not just explain
2. Report the actual results from the tool
3. Provide helpful interpretation of the data

For example, if user says 'check equipment' or 'show status', USE nina_get_status tool first. If user says 'connect all', USE nina_connect_all_equipment.";
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