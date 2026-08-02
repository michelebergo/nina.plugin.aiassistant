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
    public class ExternalMCPClient : IDisposable, IExternalMCPSource
    {
        private Process _process;
        private StreamWriter _stdin;
        private StreamReader _stdout;
        private readonly SemaphoreSlim _ioLock = new SemaphoreSlim(1, 1);
        private int _messageId = 1;

        public bool IsConnected { get; private set; }
        public string ServerName { get; private set; }
        public string ServerVersion { get; private set; }

        /// <summary>
        /// Start an external MCP server using a Python interpreter and script path.
        /// Kept for backward compatibility; delegates to the generalized overload.
        /// </summary>
        public Task<bool> StartServerAsync(string pythonPath, string scriptPath, CancellationToken ct = default)
        {
            return StartServerAsync(pythonPath, new[] { scriptPath }, null, ct);
        }

        /// <summary>
        /// Start an external MCP server process with an arbitrary command, arguments and environment.
        /// Supports python, node, npx, docker, or any executable.
        /// </summary>
        public async Task<bool> StartServerAsync(string command, IEnumerable<string> args, IDictionary<string, string>? env, CancellationToken ct = default)
        {
            try
            {
                Logger.Info($"[MCP] Starting external MCP server: {command} {string.Join(" ", args)}");

                // On Windows, npx/npm/etc. are .cmd scripts that Process.Start (UseShellExecute=false)
                // cannot launch directly, and PATH resolution does not apply PATHEXT. Resolve the real
                // executable and route .cmd/.bat through cmd.exe.
                var (fileName, prefixArgs) = ResolveExecutable(command);

                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    // UTF-8 WITHOUT a BOM. Encoding.UTF8 emits a BOM preamble on first stdin write,
                    // which corrupts the first JSON-RPC message and breaks strict parsers (e.g. the
                    // Python MCP SDK rejects the leading \ufeff).
                    StandardOutputEncoding = new UTF8Encoding(false),
                    StandardInputEncoding = new UTF8Encoding(false)
                };

                foreach (var pa in prefixArgs)
                    startInfo.ArgumentList.Add(pa);

                foreach (var arg in args)
                    startInfo.ArgumentList.Add(arg);

                if (env != null)
                {
                    foreach (var kvp in env)
                        startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                }

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
            if (!IsConnected && method != "initialize")
            {
                throw new InvalidOperationException("MCP server not connected");
            }

            // Serialize the entire write+read cycle. stdio JSON-RPC is request/response and this client
            // matches each response to the next line read, so concurrent calls must not interleave
            // (otherwise the underlying stream throws "stream is in use by a previous operation").
            await _ioLock.WaitAsync(ct);
            try
            {
                var request = new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = _messageId++,
                    ["method"] = method,
                    ["params"] = JObject.FromObject(parameters)
                };

                var requestLine = request.ToString(Formatting.None);
                Logger.Trace($"[MCP →] {requestLine}");

                await _stdin.WriteLineAsync(requestLine);
                await _stdin.FlushAsync();

                // Read response with a timeout. Without this, a hung MCP server (e.g. one stuck in
                // an HTTP retry loop) blocks ReadLineAsync forever, freezing the entire plugin.
                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                readCts.CancelAfter(TimeSpan.FromSeconds(120));

                string? responseLine;
                try
                {
                    responseLine = await _stdout.ReadLineAsync(readCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    throw new TimeoutException($"MCP server '{ServerName}' did not respond within 120 seconds (tool may be stuck in a retry loop)");
                }

                if (string.IsNullOrEmpty(responseLine))
                {
                    throw new InvalidOperationException("MCP server closed connection");
                }

                Logger.Trace($"[MCP ←] {responseLine}");

                // Defensively strip a leading BOM in case the server emits one.
                responseLine = responseLine.TrimStart('\uFEFF');

                var response = JObject.Parse(responseLine);

                if (response["error"] != null)
                {
                    var error = response["error"]["message"]?.ToString() ?? "Unknown error";
                    throw new Exception($"MCP error: {error}");
                }

                return response["result"] as JObject;
            }
            finally
            {
                _ioLock.Release();
            }
        }

        /// <summary>
        /// Resolve a command to a launchable (FileName, prefix args) pair. On Windows this applies
        /// PATHEXT so bare commands like "npx" find "npx.cmd"/"npx.exe", and routes .cmd/.bat scripts
        /// through cmd.exe (which Process.Start cannot launch directly with UseShellExecute=false).
        /// </summary>
        private static (string fileName, List<string> prefixArgs) ResolveExecutable(string command)
        {
            if (!OperatingSystem.IsWindows())
                return (command, new List<string>());

            bool hasDir = command.IndexOfAny(new[] { '/', '\\' }) >= 0;
            bool hasExt = Path.HasExtension(command);

            // Explicit path or extension provided: use as-is, but route .cmd/.bat through cmd.exe.
            if (hasDir || hasExt)
            {
                var ext0 = Path.GetExtension(command).ToLowerInvariant();
                if (ext0 == ".cmd" || ext0 == ".bat")
                    return ("cmd.exe", new List<string> { "/c", command });
                return (command, new List<string>());
            }

            // Bare command name: search PATH using PATHEXT.
            var pathExts = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var dir in paths)
            {
                foreach (var ext in pathExts)
                {
                    string candidate;
                    try { candidate = Path.Combine(dir.Trim(), command + ext); }
                    catch { continue; }

                    if (File.Exists(candidate))
                    {
                        var e = ext.ToLowerInvariant();
                        // .cmd/.bat must run via cmd.exe; pass the bare name so cmd re-resolves it
                        // (avoids quoting issues when the resolved path contains spaces).
                        if (e == ".cmd" || e == ".bat")
                            return ("cmd.exe", new List<string> { "/c", command });
                        return (candidate, new List<string>());
                    }
                }
            }

            // Not found on PATH: let cmd.exe try (handles shims and app execution aliases).
            return ("cmd.exe", new List<string> { "/c", command });
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
