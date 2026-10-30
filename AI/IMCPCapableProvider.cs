using System.Threading;
using System.Threading.Tasks;
using NINA.Plugin.AIAssistant.AI.MCP;
using NINA.Plugin.AIAssistant.MCP;

namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// A provider with a tool loop: it can be pointed at the NINA Advanced API and at
    /// external MCP servers. The chat used to reach these two methods by casting to each
    /// concrete provider class in turn, so a provider that gained tools also had to be added
    /// there by hand; now it only has to implement this.
    /// </summary>
    public interface IMCPCapableProvider : IAIProvider
    {
        Task<bool> EnableMCPAsync(MCPConfig mcpConfig, CancellationToken cancellationToken = default);
        void SetExternalMCP(IExternalMCPSource externalMcpClient);
    }
}
