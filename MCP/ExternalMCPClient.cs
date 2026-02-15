using NINA.Core.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.AIAssistant.MCP
{
    /// <summary>
    /// Client for communicating with external MCP servers via stdio
    /// </summary>
    public class ExternalMCPClient : IDisposable
    {
        private Process _process;
        private StreamWriter _stdin;
        private StreamReader _stdout;
        private readonly object _lock = new object();
        private int _messageId = 1;

        public bool IsConnected { get; private set; }
        public string ServerName { get; private set; }
        public string ServerVersion { get; private set; }

        /// <summary>
        /// Start an external MCP server process
        /// </summary>
        public async Task<bool> StartServerAsync(string pythonPath, string scriptPath, CancellationToken ct = default)
        {
            try
            {
                Logger.Info($"[MCP] Starting external MCP server: {scriptPath}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = scriptPath,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardInputEncoding = Encoding.UTF8
                };

                _process = new Process { StartInfo = startInfo };
                
                // Capture stderr for debugging
                _process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Logger.Warning($"[MCP Server stderr] {e.Data}");
                    }
                };

                _process.Start();
                _process.BeginErrorReadLine();

                _stdin = _process.StandardInput;
                _stdout = _process.StandardOutput;

                // Initialize the MCP connection
                var initResult = await SendRequestAsync("initialize", new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new { },
                    clientInfo = new
                    {
                        name = "NINA.Plugin.AIAssistant",
                        version = "1.0.0"
                    }
                }, ct);

                if (initResult != null)
                {
                    ServerName = initResult["serverInfo"]?["name"]?.ToString() ?? "Unknown";
                    ServerVersion = initResult["serverInfo"]?["version"]?.ToString() ?? "Unknown";
                    IsConnected = true;
                    Logger.Info($"[MCP] Connected to {ServerName} v{ServerVersion}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"[MCP] Failed to start server: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get list of available tools from the MCP server
        /// </summary>
        public async Task<List<JObject>> GetToolsAsync(CancellationToken ct = default)
        {
            try
            {
                var result = await SendRequestAsync("tools/list", new { }, ct);
                var tools = result?["tools"]?.ToObject<List<JObject>>();
                
                if (tools != null)
                {
                    Logger.Info($"[MCP] Retrieved {tools.Count} tools from external server");
                    return tools;
                }

                return new List<JObject>();
            }
            catch (Exception ex)
            {
                Logger.Error($"[MCP] Failed to get tools: {ex.Message}");
                return new List<JObject>();
            }
        }

        /// <summary>
        /// Call a tool on the external MCP server
        /// </summary>
        public async Task<JObject> CallToolAsync(string toolName, JObject arguments, CancellationToken ct = default)
        {
            try
            {
                Logger.Info($"[MCP] Calling external tool: {toolName}");

                var result = await SendRequestAsync("tools/call", new
                {
                    name = toolName,
                    arguments = arguments
                }, ct);

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"[MCP] Tool call failed: {ex.Message}");
                return new JObject
                {
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Send a JSON-RPC request to the MCP server
        /// </summary>
        private async Task<JObject> SendRequestAsync(string method, object parameters, CancellationToken ct)
        {
            lock (_lock)
            {
                if (!IsConnected && method != "initialize")
                {
                    throw new InvalidOperationException("MCP server not connected");
                }

                var request = new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = _messageId++,
                    ["method"] = method,
                    ["params"] = JObject.FromObject(parameters)
                };

                var requestLine = request.ToString(Formatting.None);
                Logger.Trace($"[MCP →] {requestLine}");

                _stdin.WriteLine(requestLine);
                _stdin.Flush();
            }

            // Read response (may need timeout handling)
            var responseLine = await _stdout.ReadLineAsync();
            
            if (string.IsNullOrEmpty(responseLine))
            {
                throw new InvalidOperationException("MCP server closed connection");
            }

            Logger.Trace($"[MCP ←] {responseLine}");

            var response = JObject.Parse(responseLine);

            if (response["error"] != null)
            {
                var error = response["error"]["message"]?.ToString() ?? "Unknown error";
                throw new Exception($"MCP error: {error}");
            }

            return response["result"] as JObject;
        }

        public void Dispose()
        {
            try
            {
                IsConnected = false;

                _stdin?.Close();
                _stdout?.Close();

                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                    _process.Dispose();
                }

                Logger.Info("[MCP] External MCP server disconnected");
            }
            catch (Exception ex)
            {
                Logger.Error($"[MCP] Error during disposal: {ex.Message}");
            }
        }
    }
}
