using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NINA.Core.Utility;
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
    /// The conversation loop for tool calling on /chat/completions: send the messages, run
    /// every tool the model asked for, append the results, send again, until the model answers
    /// in words or the iteration cap is hit. It owns no HTTP client and no NINA connection -
    /// the provider hands in how to post and how to invoke - so the whole loop runs under test
    /// with scripted responses.
    /// </summary>
    public sealed class ChatCompletionsToolLoop
    {
        private readonly Func<IReadOnlyList<object>, CancellationToken, Task<(HttpStatusCode Status, string Body)>> _post;
        private readonly Func<string, Dictionary<string, object>, CancellationToken, Task<MCPToolResult>> _invoke;
        private readonly Action<string>? _progress;
        private readonly bool _includeToolNameInResult;
        private readonly int _maxIterations;

        public ChatCompletionsToolLoop(
            Func<IReadOnlyList<object>, CancellationToken, Task<(HttpStatusCode Status, string Body)>> post,
            Func<string, Dictionary<string, object>, CancellationToken, Task<MCPToolResult>> invoke,
            Action<string>? progress,
            bool includeToolNameInResult,
            int maxIterations = 10)
        {
            _post = post;
            _invoke = invoke;
            _progress = progress;
            _includeToolNameInResult = includeToolNameInResult;
            _maxIterations = maxIterations;
        }

        public async Task<ToolLoopOutcome> RunAsync(List<object> messages, CancellationToken cancellationToken)
        {
            var allToolResults = new List<string>();
            int iterations = 0;

            while (iterations < _maxIterations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                iterations++;
                _progress?.Invoke($"🔧 Iteration {iterations} — thinking...");

                var (status, body) = await _post(messages, cancellationToken);
                if ((int)status < 200 || (int)status >= 300)
                {
                    Logger.Error($"Chat completions API error {(int)status}: {body}");
                    return new ToolLoopOutcome
                    {
                        Succeeded = false,
                        Error = $"API Error: {(int)status} ({status}) - {body}",
                        ToolResults = allToolResults,
                        Iterations = iterations
                    };
                }

                var response = JObject.Parse(body);
                var toolCalls = ChatCompletionsTools.ParseToolCalls(response);

                if (toolCalls.Count == 0)
                {
                    return new ToolLoopOutcome
                    {
                        Succeeded = true,
                        FinalResponse = response,
                        ToolResults = allToolResults,
                        Iterations = iterations
                    };
                }

                var assistantMessage = ChatCompletionsTools.Message(response)!;
                messages.Add(ChatCompletionsTools.AssistantEcho(assistantMessage));

                var executions = toolCalls.Select(async call =>
                {
                    Logger.Info($"[MCP] Executing tool: {call.Name}");
                    _progress?.Invoke($"🔧 Calling: {call.Name}{MCPToolTrace.DescribeArguments(call.Arguments)}");

                    MCPToolResult result;
                    try
                    {
                        result = await _invoke(call.Name, call.Arguments, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        result = new MCPToolResult { Success = false, Error = ex.Message };
                    }

                    Logger.Info($"[MCP] Tool {call.Name} completed - Success: {result.Success}");
                    _progress?.Invoke(MCPToolTrace.DescribeOutcome(result.Success, result.Content, result.Error));

                    var content = result.Success
                        ? result.Content ?? "Tool executed successfully"
                        : $"Error: {result.Error}";

                    return (call, result.Success, message: ChatCompletionsTools.ToolResult(call.Id, call.Name, content, _includeToolNameInResult));
                }).ToList();

                var completed = await Task.WhenAll(executions);
                foreach (var (call, success, message) in completed)
                {
                    allToolResults.Add($"{call.Name}: {(success ? "Success" : "Failed")}");
                    messages.Add(message);
                }
            }

            return new ToolLoopOutcome
            {
                Succeeded = false,
                Error = "Maximum tool iterations reached",
                ToolResults = allToolResults,
                Iterations = iterations
            };
        }
    }
}
