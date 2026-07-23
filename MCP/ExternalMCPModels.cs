using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NINA.Core.Utility;

namespace NINA.Plugin.AIAssistant.MCP
{
    /// <summary>
    /// Common surface for an external MCP tool source. Implemented by both a single
    /// <see cref="ExternalMCPClient"/> and the multi-server <see cref="ExternalMCPManager"/>,
    /// so AI providers can consume one or many external MCP servers transparently.
    /// </summary>
    public interface IExternalMCPSource
    {
        bool IsConnected { get; }
        string ServerName { get; }
        Task<List<JObject>> GetToolsAsync(CancellationToken ct = default);
        Task<JObject> CallToolAsync(string toolName, JObject arguments, CancellationToken ct = default);
    }

    /// <summary>
    /// Configuration for a single external MCP server (standard mcpServers format).
    /// </summary>
    public class ExternalMCPServerConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public List<string> Args { get; set; } = new List<string>();
        public Dictionary<string, string> Env { get; set; } = new Dictionary<string, string>();
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Parses the standard MCP <c>mcpServers</c> JSON configuration into a list of server configs.
    /// Accepts either a wrapper object <c>{ "mcpServers": { ... } }</c> or a bare map <c>{ "name": { ... } }</c>.
    /// </summary>
    public static class ExternalMCPConfigParser
    {
        public static List<ExternalMCPServerConfig> Parse(string? json)
        {
            var result = new List<ExternalMCPServerConfig>();
            if (string.IsNullOrWhiteSpace(json))
                return result;

            try
            {
                var root = JToken.Parse(json);

                // Support both { "mcpServers": { ... } } and a bare { "name": { ... } } map.
                JObject? servers = (root["mcpServers"] as JObject) ?? (root as JObject);
                if (servers == null)
                    return result;

                foreach (var prop in servers.Properties())
                {
                    // Skip the wrapper key itself if a bare map happens to contain it.
                    if (prop.Name == "mcpServers")
                        continue;

                    if (prop.Value is not JObject serverObj)
                        continue;

                    var command = serverObj["command"]?.ToString();
                    if (string.IsNullOrWhiteSpace(command))
                    {
                        Logger.Warning($"[MCP] Server '{prop.Name}' has no 'command'; skipping.");
                        continue;
                    }

                    var config = new ExternalMCPServerConfig
                    {
                        Name = prop.Name,
                        Command = command,
                        Enabled = serverObj["enabled"]?.Value<bool?>() ?? true
                    };

                    if (serverObj["args"] is JArray argsArray)
                    {
                        foreach (var arg in argsArray)
                            config.Args.Add(arg.ToString());
                    }

                    if (serverObj["env"] is JObject envObj)
                    {
                        foreach (var envProp in envObj.Properties())
                            config.Env[envProp.Name] = envProp.Value.ToString();
                    }

                    result.Add(config);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[MCP] Failed to parse external MCP server config: {ex.Message}");
            }

            return result;
        }
    }
}
