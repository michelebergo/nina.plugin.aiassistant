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
    /// <summary>
    /// How one API spells tool calling: where the calls are in a response, what to replay
    /// before the results, and how the results of one round are written - some APIs want one
    /// item per result, Anthropic and Gemini want them bundled in a single message. The loop
    /// itself is the same for all.
    /// </summary>
    public interface IToolDialect
    {
        List<ChatCompletionsToolCall> ParseCalls(JObject response);
        IEnumerable<object> Echo(JObject response);
        IEnumerable<object> Results(IReadOnlyList<(ChatCompletionsToolCall Call, string Content)> results);
    }

    /// <summary>
    /// The conversation loop for tool calling: send the items, run every tool the model asked
    /// for, append the results, send again, until the model answers in words or the iteration
    /// cap is hit. It owns no HTTP client and no NINA connection - the provider hands in how
    /// to post and how to invoke - so the whole loop runs under test with scripted responses.
    /// </summary>
    public class ToolLoop
    {
        private readonly IToolDialect _dialect;
        private readonly Func<IReadOnlyList<object>, CancellationToken, Task<(HttpStatusCode Status, string Body)>> _post;
        private readonly Func<string, Dictionary<string, object>, CancellationToken, Task<MCPToolResult>> _invoke;
        private readonly Action<string>? _progress;
        private readonly int _maxIterations;

        public ToolLoop(
            IToolDialect dialect,
            Func<IReadOnlyList<object>, CancellationToken, Task<(HttpStatusCode Status, string Body)>> post,
            Func<string, Dictionary<string, object>, CancellationToken, Task<MCPToolResult>> invoke,
            Action<string>? progress,
            int maxIterations = 10)
        {
            _dialect = dialect;
            _post = post;
            _invoke = invoke;
            _progress = progress;
            _maxIterations = maxIterations;
        }

        public async Task<ToolLoopOutcome> RunAsync(List<object> items, CancellationToken cancellationToken)
        {
            var allToolResults = new List<string>();
            int iterations = 0;

            while (iterations < _maxIterations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                iterations++;
                _progress?.Invoke($"🔧 Iteration {iterations} — thinking...");

                var (status, body) = await _post(items, cancellationToken);
                if ((int)status < 200 || (int)status >= 300)
                {
                    Logger.Error($"Tool loop API error {(int)status}: {body}");
                    return new ToolLoopOutcome
                    {
                        Succeeded = false,
                        Error = $"API Error: {(int)status} ({status}) - {body}",
                        ToolResults = allToolResults,
                        Iterations = iterations
                    };
                }

                var response = JObject.Parse(body);
                var calls = _dialect.ParseCalls(response);

                if (calls.Count == 0)
                {
                    return new ToolLoopOutcome
                    {
                        Succeeded = true,
                        FinalResponse = response,
                        ToolResults = allToolResults,
                        Iterations = iterations
                    };
                }

                items.AddRange(_dialect.Echo(response));

                var executions = calls.Select(async call =>
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

                    return (call, result.Success, content);
                }).ToList();

                var completed = await Task.WhenAll(executions);
                foreach (var (call, success, _) in completed)
                {
                    allToolResults.Add($"{call.Name}: {(success ? "Success" : "Failed")}");
                }
                items.AddRange(_dialect.Results(completed.Select(c => (c.call, c.content)).ToList()));
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

    /// <summary>
    /// The /chat/completions spelling (OpenAI, Mistral): calls under choices[0].message,
    /// the assistant message replayed before the results, one role "tool" message per result.
    /// </summary>
    public sealed class ChatCompletionsDialect : IToolDialect
    {
        private readonly bool _includeToolNameInResult;
        public ChatCompletionsDialect(bool includeToolNameInResult) { _includeToolNameInResult = includeToolNameInResult; }

        public List<ChatCompletionsToolCall> ParseCalls(JObject response) => ChatCompletionsTools.ParseToolCalls(response);
        public IEnumerable<object> Echo(JObject response) => new[] { ChatCompletionsTools.AssistantEcho(ChatCompletionsTools.Message(response)!) };
        public IEnumerable<object> Results(IReadOnlyList<(ChatCompletionsToolCall Call, string Content)> results) =>
            results.Select(r => ChatCompletionsTools.ToolResult(r.Call.Id, r.Call.Name, r.Content, _includeToolNameInResult));
    }
}
