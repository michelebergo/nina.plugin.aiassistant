using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NINA.Plugin.AIAssistant.AI;
using NINA.Plugin.AIAssistant.AI.MCP;
using NINA.Plugin.AIAssistant.MCP;
using NUnit.Framework;

namespace NINA.Plugin.AIAssistant.Test {

    /// <summary>
    /// The real request round trip against each provider's real API: tools declared, the model
    /// calls one, the result goes back in the provider's own dialect, the model answers from
    /// it. Unit tests encode what we believe the API accepts; these ask the API. Two defects
    /// on 2026-10-31 (a JSON Schema type list reaching Gemini, Gemini refusing a "function"
    /// role) passed every unit test and failed here.
    ///
    /// Opt-in: NUnit runs [Explicit] fixtures only when selected, e.g.
    ///   dotnet test --filter Category=Live
    /// Needs the keys in the environment (AIASSISTANT_OPENAI_KEY, AIASSISTANT_ANTHROPIC_KEY,
    /// AIASSISTANT_GOOGLE_KEY, AIASSISTANT_MISTRAL_KEY), NINA open with the Advanced API on
    /// localhost:1888 (the providers only take the tool path with it), and Ollama reachable for
    /// its test. Optional overrides: AIASSISTANT_&lt;PROVIDER&gt;_MODEL, AIASSISTANT_OPENAI_RESPONSES_MODEL,
    /// AIASSISTANT_OLLAMA_ENDPOINT, AIASSISTANT_NINA_HOST / _PORT. Each test costs one or two
    /// short requests.
    /// </summary>
    [TestFixture]
    [Explicit("Calls the real provider APIs; run with --filter Category=Live")]
    [Category("Live")]
    public class LiveProviderContractTest {

        /// <summary>A word the model can only know from the tool's answer.</summary>
        private const string Token = "ZX-4471";

        /// <summary>
        /// One external tool whose schema has the shapes real servers write and strict APIs
        /// refuse when passed through untouched: a type list, an array without items, a
        /// nested object. It sits among the 159 built-in tools, as in production.
        /// </summary>
        private sealed class BenchSource : IExternalMCPSource {
            public int Calls;
            public JObject? LastArguments;
            public bool IsConnected => true;
            public string ServerName => "bench";

            public Task<List<JObject>> GetToolsAsync(CancellationToken ct = default) {
                var tool = JObject.Parse(@"{
                    ""name"": ""bench_echo"",
                    ""description"": ""Test bench. Echoes the text it is given and adds the bench answer."",
                    ""inputSchema"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""text"": { ""type"": ""string"", ""description"": ""The text to echo"" },
                            ""loud"": { ""type"": [""boolean"", ""string""], ""description"": ""Optional emphasis"" },
                            ""tags"": { ""type"": ""array"", ""description"": ""Optional labels"" },
                            ""meta"": { ""type"": ""object"", ""properties"": { ""n"": { ""type"": ""integer"" } } }
                        },
                        ""required"": [""text""]
                    }
                }");
                return Task.FromResult(new List<JObject> { tool });
            }

            public Task<JObject> CallToolAsync(string toolName, JObject arguments, CancellationToken ct = default) {
                Calls++;
                LastArguments = arguments;
                var text = arguments["text"]?.ToString() ?? "";
                return Task.FromResult(JObject.Parse(
                    $@"{{ ""content"": [ {{ ""type"": ""text"", ""text"": ""bench_echo received '{text}' and answers {Token}"" }} ] }}"));
            }
        }

        private static string? Env(string name) => Environment.GetEnvironmentVariable(name);

        private static string Key(string name) {
            var value = Env(name);
            if (string.IsNullOrWhiteSpace(value)) Assert.Ignore($"{name} is not set");
            return value!;
        }

        private static async Task<AIResponse> RoundTripAsync(IMCPCapableProvider provider, AIProviderConfig config) {
            (await provider.InitializeAsync(config)).Should().BeTrue("the provider must accept its configuration");

            var nina = new MCPConfig {
                NinaHost = Env("AIASSISTANT_NINA_HOST") ?? "localhost",
                NinaPort = int.TryParse(Env("AIASSISTANT_NINA_PORT"), out var port) ? port : 1888
            };
            if (!await provider.EnableMCPAsync(nina))
                Assert.Ignore($"NINA Advanced API not reachable at {nina.NinaHost}:{nina.NinaPort} - open NINA with the Advanced API plugin");

            var bench = new BenchSource();
            provider.SetExternalMCP(bench);

            var progress = new List<string>();
            var response = await provider.SendRequestAsync(new AIRequest {
                Prompt = "Call the tool bench_echo with text=\"ping\" and report its answer word for word. Do not call any other tool.",
                MaxTokens = 400,
                ProgressCallback = progress.Add
            });

            var trace = string.Join(" | ", progress);
            response.Success.Should().BeTrue($"error: {response.Error}; trace: {trace}");
            bench.Calls.Should().BeGreaterThan(0, $"the model should have called bench_echo; trace: {trace}");
            bench.LastArguments!["text"]!.ToString().Should().Be("ping");
            response.Content.Should().Contain(Token, $"the final answer must come from the tool result; trace: {trace}");
            return response;
        }

        [Test]
        public async Task OpenAI_ChatCompletions() {
            await RoundTripAsync(new OpenAIProvider(), new AIProviderConfig {
                Provider = AIProviderType.OpenAI,
                ApiKey = Key("AIASSISTANT_OPENAI_KEY"),
                ModelId = Env("AIASSISTANT_OPENAI_MODEL") ?? "gpt-4.1-mini"
            });
        }

        [Test]
        public async Task OpenAI_ResponsesApi() {
            // A model that calls tools only on /v1/responses: the provider must notice and switch.
            var response = await RoundTripAsync(new OpenAIProvider(), new AIProviderConfig {
                Provider = AIProviderType.OpenAI,
                ApiKey = Key("AIASSISTANT_OPENAI_KEY"),
                ModelId = Env("AIASSISTANT_OPENAI_RESPONSES_MODEL") ?? "gpt-6-astra"
            });
            response.Metadata.Should().ContainKey("api").WhoseValue.Should().Be("responses");
        }

        [Test]
        public async Task Anthropic_Messages() {
            var key = Key("AIASSISTANT_ANTHROPIC_KEY");
            var model = Env("AIASSISTANT_ANTHROPIC_MODEL") ?? await CheapestListedAsync(new AnthropicProvider(), AIProviderType.Anthropic, key, "haiku");
            await RoundTripAsync(new AnthropicProvider(), new AIProviderConfig {
                Provider = AIProviderType.Anthropic, ApiKey = key, ModelId = model
            });
        }

        [Test]
        public async Task Google_GenerateContent() {
            await RoundTripAsync(new GoogleProvider(), new AIProviderConfig {
                Provider = AIProviderType.Google,
                ApiKey = Key("AIASSISTANT_GOOGLE_KEY"),
                ModelId = Env("AIASSISTANT_GOOGLE_MODEL") ?? "gemini-flash-latest"
            });
        }

        [Test]
        public async Task Mistral_ChatCompletions() {
            await RoundTripAsync(new MistralProvider(), new AIProviderConfig {
                Provider = AIProviderType.Mistral,
                ApiKey = Key("AIASSISTANT_MISTRAL_KEY"),
                ModelId = Env("AIASSISTANT_MISTRAL_MODEL") ?? "mistral-small-latest"
            });
        }

        [Test]
        public async Task Ollama_Chat() {
            var endpoint = Env("AIASSISTANT_OLLAMA_ENDPOINT") ?? "http://localhost:11434";
            var model = Env("AIASSISTANT_OLLAMA_MODEL");
            if (model == null) {
                var probe = new OllamaProvider();
                await probe.InitializeAsync(new AIProviderConfig { Provider = AIProviderType.Ollama, Endpoint = endpoint });
                model = (await probe.GetAvailableModelsAsync()).FirstOrDefault();
                if (model == null) Assert.Ignore($"Ollama at {endpoint} is not reachable or has no models");
            }
            await RoundTripAsync(new OllamaProvider(), new AIProviderConfig {
                Provider = AIProviderType.Ollama, Endpoint = endpoint, ModelId = model
            });
        }

        /// <summary>The model to spend on: the first listed one whose id contains the hint, else the first.</summary>
        private static async Task<string> CheapestListedAsync(IAIProvider probe, AIProviderType type, string key, string hint) {
            await probe.InitializeAsync(new AIProviderConfig { Provider = type, ApiKey = key });
            var models = await probe.GetAvailableModelsAsync();
            if (models.Length == 0) Assert.Ignore($"{type}: the model list came back empty");
            return models.FirstOrDefault(m => m.Contains(hint, StringComparison.OrdinalIgnoreCase)) ?? models[0];
        }
    }
}
