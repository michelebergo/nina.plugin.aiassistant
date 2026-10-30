using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NINA.Plugin.AIAssistant.AI.MCP;

namespace NINA.Plugin.AIAssistant.AI
{
    public sealed class ToolLoopOutcome
    {
        public bool Succeeded { get; init; }
        public JObject? FinalResponse { get; init; }
        public string? Error { get; init; }
        public List<string> ToolResults { get; init; } = new();
        public int Iterations { get; init; }
    }

    /// <summary>
    /// The tool loop in the /chat/completions spelling, shared by OpenAI and Mistral. A thin
    /// name over <see cref="ToolLoop"/> with <see cref="ChatCompletionsDialect"/>.
    /// </summary>
    public sealed class ChatCompletionsToolLoop : ToolLoop
    {
        public ChatCompletionsToolLoop(
            Func<IReadOnlyList<object>, CancellationToken, Task<(HttpStatusCode Status, string Body)>> post,
            Func<string, Dictionary<string, object>, CancellationToken, Task<MCPToolResult>> invoke,
            Action<string>? progress,
            bool includeToolNameInResult,
            int maxIterations = 10)
            : base(new ChatCompletionsDialect(includeToolNameInResult), post, invoke, progress, maxIterations)
        {
        }
    }
}
