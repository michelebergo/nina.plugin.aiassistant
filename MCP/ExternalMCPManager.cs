using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NINA.Core.Utility;

namespace NINA.Plugin.AIAssistant.MCP
{
    /// <summary>
    /// Manages multiple external MCP servers as a single tool source. Aggregates tools from all
    /// connected servers, prefixing tool names with a sanitized server name on collision to avoid
    /// silent drops, and routes tool calls to the owning server.
    /// </summary>
    public class ExternalMCPManager : IDisposable, IExternalMCPSource
    {
        private readonly List<(string name, ExternalMCPClient client)> _clients = new List<(string, ExternalMCPClient)>();

        // Maps the exposed (possibly prefixed) tool name -> owning client + raw tool name.
        private readonly Dictionary<string, (ExternalMCPClient client, string rawName)> _routingMap =
            new Dictionary<string, (ExternalMCPClient, string)>(StringComparer.OrdinalIgnoreCase);

        private readonly object _mapLock = new object();

        public bool IsConnected => _clients.Any(c => c.client.IsConnected);

        public string ServerName
        {
            get
            {
                var connected = _clients.Where(c => c.client.IsConnected).Select(c => c.name).ToList();
                return connected.Count switch
                {
                    0 => "None",
                    1 => connected[0],
                    _ => $"{connected.Count} servers ({string.Join(", ", connected)})"
                };
            }
        }

        /// <summary>
        /// Number of currently connected servers.
        /// </summary>
        public int ConnectedCount => _clients.Count(c => c.client.IsConnected);

        /// <summary>
        /// Start all enabled servers. Returns true if at least one server connected.
        /// </summary>
        public async Task<bool> StartAllAsync(IEnumerable<ExternalMCPServerConfig> configs, CancellationToken ct = default)
        {
            foreach (var config in configs)
            {
                if (!config.Enabled)
                {
                    Logger.Info($"[MCP] Skipping disabled external server '{config.Name}'");
                    continue;
                }

                var client = new ExternalMCPClient();
                try
                {
                    var started = await client.StartServerAsync(config.Command, config.Args, config.Env, ct);
                    if (started)
                    {
                        _clients.Add((config.Name, client));
                        Logger.Info($"[MCP] External server '{config.Name}' connected ({client.ServerName} v{client.ServerVersion})");
                    }
                    else
                    {
                        Logger.Warning($"[MCP] External server '{config.Name}' failed to start");
                        client.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"[MCP] Error starting external server '{config.Name}': {ex.Message}");
                    client.Dispose();
                }
            }

            Logger.Info($"[MCP] External MCP manager: {ConnectedCount}/{_clients.Count} server(s) connected");
            return IsConnected;
        }

        /// <summary>
        /// Aggregate tools from all connected servers, building the routing map. Tool names that
        /// collide across servers are prefixed with a sanitized server name.
        /// </summary>
        public async Task<List<JObject>> GetToolsAsync(CancellationToken ct = default)
        {
            var aggregated = new List<JObject>();
            var newMap = new Dictionary<string, (ExternalMCPClient, string)>(StringComparer.OrdinalIgnoreCase);

            foreach (var (name, client) in _clients)
            {
                if (!client.IsConnected) continue;

                List<JObject> tools;
                try
                {
                    tools = await client.GetToolsAsync(ct);
                }
                catch (Exception ex)
                {
                    Logger.Error($"[MCP] Failed to list tools from '{name}': {ex.Message}");
                    continue;
                }

                foreach (var tool in tools)
                {
                    var rawName = tool["name"]?.ToString();
                    if (string.IsNullOrEmpty(rawName)) continue;

                    // If the raw name is already taken by another server, namespace it.
                    var exposedName = rawName!;
                    if (newMap.ContainsKey(exposedName))
                        exposedName = $"{Sanitize(name)}__{rawName}";

                    // Still colliding (same server name + tool repeated) -> ensure uniqueness.
                    var uniqueName = exposedName;
                    int suffix = 2;
                    while (newMap.ContainsKey(uniqueName))
                        uniqueName = $"{exposedName}_{suffix++}";
                    exposedName = uniqueName;

                    var clone = (JObject)tool.DeepClone();
                    clone["name"] = exposedName;
                    aggregated.Add(clone);
                    newMap[exposedName] = (client, rawName!);
                }
            }

            lock (_mapLock)
            {
                _routingMap.Clear();
                foreach (var kvp in newMap)
                    _routingMap[kvp.Key] = kvp.Value;
            }

            Logger.Info($"[MCP] External MCP manager aggregated {aggregated.Count} tool(s) from {ConnectedCount} server(s)");
            return aggregated;
        }

        /// <summary>
        /// Route a tool call to the server that owns the (possibly prefixed) tool name.
        /// </summary>
        public async Task<JObject> CallToolAsync(string toolName, JObject arguments, CancellationToken ct = default)
        {
            (ExternalMCPClient client, string rawName) route;
            bool found;
            lock (_mapLock)
            {
                found = _routingMap.TryGetValue(toolName, out route);
            }

            // If the map isn't populated yet (CallTool before GetTools), build it.
            if (!found)
            {
                await GetToolsAsync(ct);
                lock (_mapLock)
                {
                    found = _routingMap.TryGetValue(toolName, out route);
                }
            }

            if (!found)
            {
                return new JObject { ["error"] = $"Tool '{toolName}' not found in any external MCP server" };
            }

            return await route.client.CallToolAsync(route.rawName, arguments, ct);
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "server";
            return Regex.Replace(name, "[^A-Za-z0-9_]", "_");
        }

        public void Dispose()
        {
            foreach (var (name, client) in _clients)
            {
                try { client.Dispose(); }
                catch (Exception ex) { Logger.Error($"[MCP] Error disposing external server '{name}': {ex.Message}"); }
            }
            _clients.Clear();
            lock (_mapLock) { _routingMap.Clear(); }
        }
    }
}
