using System.Collections.Generic;

namespace NINA.Plugin.AIAssistant.AI.MCP
{
    /// <summary>
    /// Configuration for MCP (Model Context Protocol) integration
    /// </summary>
    public class MCPConfig
    {
        /// <summary>
        /// Whether MCP is enabled for tool-capable AI providers
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// NINA Advanced API host address
        /// </summary>
        public string NinaHost { get; set; } = "localhost";

        /// <summary>
        /// NINA Advanced API port
        /// </summary>
        public int NinaPort { get; set; } = 1888;

        /// <summary>
        /// Directory where captured images should be saved
        /// </summary>
        public string ImageSaveDir { get; set; } = "";

        /// <summary>
        /// Path to the MCP server script (nina_advanced_mcp.py)
        /// </summary>
        public string MCPServerPath { get; set; } = "";
    }

    /// <summary>
    /// Represents an MCP tool definition for Claude
    /// </summary>
    public class MCPTool
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public MCPToolInputSchema InputSchema { get; set; } = new();
    }

    /// <summary>
    /// JSON Schema for MCP tool inputs
    /// </summary>
    public class MCPToolInputSchema
    {
        public string Type { get; set; } = "object";
        public Dictionary<string, MCPToolParameter> Properties { get; set; } = new();
        public List<string> Required { get; set; } = new();
    }

    /// <summary>
    /// Parameter definition for an MCP tool
    /// </summary>
    public class MCPToolParameter
    {
        public string Type { get; set; } = "string";
        public string? Description { get; set; }
        public string? Default { get; set; }
        public List<string>? Enum { get; set; }
    }

    /// <summary>
    /// Result from an MCP tool invocation
    /// </summary>
    public class MCPToolResult
    {
        public bool Success { get; set; }
        public string? Content { get; set; }
        public string? Error { get; set; }
        public Dictionary<string, object>? Data { get; set; }
    }

    /// <summary>
    /// Request to invoke a tool via the NINA Advanced API
    /// </summary>
    public class MCPToolRequest
    {
        public string ToolName { get; set; } = string.Empty;
        public Dictionary<string, object>? Arguments { get; set; }
    }
}
