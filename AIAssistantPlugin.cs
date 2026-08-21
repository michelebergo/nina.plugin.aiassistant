using NINA.Plugin.AIAssistant.Properties;
using NINA.Core.Utility;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NINA.Plugin.AIAssistant.AI;
using NINA.Plugin.AIAssistant.Orchestrator;

namespace NINA.Plugin.AIAssistant
{
    [Export(typeof(IPluginManifest))]
    public class AIAssistantPlugin : PluginBase, INotifyPropertyChanged
    {
        private readonly IProfileService profileService;
        private readonly AIService aiService;

        public static AIAssistantPlugin? Instance { get; private set; }

        // Centralized external links (single point of truth; see .github/FUNDING.yml and README)
        public const string BuyMeACoffeeUrl = "https://buymeacoffee.com/michelebergo";

        public System.Windows.Input.ICommand OpenSupportPageCommand { get; } = new NINA.Core.Utility.RelayCommand(_ => OpenExternalUrl(BuyMeACoffeeUrl));

        private static void OpenExternalUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Unable to open external URL: {uri}", ex);
            }
        }

        private AIProviderType _selectedProvider;
        public AIProviderType SelectedProviderInternal
        {
            get => _selectedProvider;
            set
            {
                if (_selectedProvider != value)
                {
                    _selectedProvider = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsGitHubSelected));
                    RaisePropertyChanged(nameof(IsOpenAISelected));
                    RaisePropertyChanged(nameof(IsAnthropicSelected));
                    RaisePropertyChanged(nameof(IsGoogleSelected));
                    RaisePropertyChanged(nameof(IsOllamaSelected));
                    RaisePropertyChanged(nameof(IsMistralSelected));
                    RaisePropertyChanged(nameof(IsMCPProviderSelected));
                }
            }
        }

        public bool IsGitHubSelected => SelectedProviderInternal == AIProviderType.GitHub;
        public bool IsOpenAISelected => SelectedProviderInternal == AIProviderType.OpenAI;
        public bool IsAnthropicSelected => SelectedProviderInternal == AIProviderType.Anthropic;
        public bool IsGoogleSelected => SelectedProviderInternal == AIProviderType.Google;
        public bool IsOllamaSelected => SelectedProviderInternal == AIProviderType.Ollama;
        public bool IsMistralSelected => SelectedProviderInternal == AIProviderType.Mistral;
        public bool IsMCPProviderSelected => SelectedProviderInternal == AIProviderType.Anthropic || 
                                              SelectedProviderInternal == AIProviderType.Google || 
                                              SelectedProviderInternal == AIProviderType.Ollama;

        [ImportingConstructor]
        public AIAssistantPlugin(IProfileService profileService, 
            [ImportMany] IEnumerable<NINA.Equipment.Interfaces.ViewModel.IDockableVM> dockables,
            [ImportMany] IEnumerable<System.Windows.ResourceDictionary> resourceDictionaries)
        {
            Instance = this;
            this.profileService = profileService;

            Logger.Info($"Plugin constructor: Found {dockables.Count()} dockable panels");
            foreach (var dockable in dockables)
            {
                Logger.Info($"Dockable found: {dockable.Title} (ContentId: {dockable.ContentId})");
            }

            // Merge resource dictionaries into application resources
            Logger.Info($"Plugin constructor: Found {resourceDictionaries.Count()} resource dictionaries");
            foreach (var dict in resourceDictionaries)
            {
                System.Windows.Application.Current?.Resources.MergedDictionaries.Add(dict);
                Logger.Info($"Merged resource dictionary: {dict.GetType().Name}");
            }

            if (Settings.Default.UpdateSettings)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Settings.Default);
            }

            this.aiService = new AIService();

            // Initialize selected provider
            _ = InitializeAIProviderAsync();

            Logger.Info("NINA ai assistant Plugin loaded successfully");
        }

        private async Task InitializeAIProviderAsync()
        {
            try
            {
                var config = GetCurrentProviderConfig();
                if (config != null && !string.IsNullOrEmpty(config.ApiKey) || config?.Provider == AIProviderType.Ollama)
                {
                    await aiService.InitializeAsync(config);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize AI provider: {ex.Message}");
            }
        }

        /// <summary>
        /// Editing one field in the options panel writes several settings in a burst - a
        /// provider change alone touches the provider, its model and the model list - and
        /// each setter used to launch its own fire-and-forget initialization. Eight of them
        /// would run concurrently with no ordering guarantee, so the configuration that
        /// ended up active was the one that happened to finish last, not the one the user
        /// chose last. The burst is now coalesced into a single initialization, and the
        /// gate keeps two of them from overlapping.
        /// </summary>
        private void ScheduleProviderInitialization()
        {
            var cts = new CancellationTokenSource();
            // Cancel the pending one only after the replacement is in place, and let the
            // superseded task dispose its own source: cancelling and disposing here could
            // pull the token out from under a delay that is still registered on it.
            var previous = Interlocked.Exchange(ref _initializationCts, cts);
            previous?.Cancel();

            _ = RunScheduledInitializationAsync(cts);
        }

        private async Task RunScheduledInitializationAsync(CancellationTokenSource cts)
        {
            try
            {
                var token = cts.Token;
                await Task.Delay(SettingsBurstWindow, token);
                await _initializationGate.WaitAsync(token);
                try
                {
                    token.ThrowIfCancellationRequested();
                    await InitializeAIProviderAsync();
                }
                finally
                {
                    _initializationGate.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer settings change; the newer one will initialize.
            }
            finally
            {
                Interlocked.CompareExchange(ref _initializationCts, null, cts);
                cts.Dispose();
            }
        }

        /// <summary>
        /// A model change does not touch the connection, so it must not rebuild it: the
        /// model id is read from the configuration when each request is built. Only a
        /// provider that has not been initialized yet needs the full path.
        /// </summary>
        private void ApplyModelSelection(AIProviderType provider, string? modelId)
        {
            RaisePropertyChanged(nameof(SelectedModelId));

            if (!aiService.TryUpdateModel(provider, modelId))
            {
                ScheduleProviderInitialization();
            }
        }

        /// <summary>How long to wait for a burst of settings writes to settle.</summary>
        private static readonly TimeSpan SettingsBurstWindow = TimeSpan.FromMilliseconds(400);

        private CancellationTokenSource? _initializationCts;
        private readonly SemaphoreSlim _initializationGate = new SemaphoreSlim(1, 1);

        private AIProviderConfig? GetCurrentProviderConfig()
        {
            var provider = SelectedProvider;
            
            return provider switch
            {
                AIProviderType.GitHub => new AIProviderConfig
                {
                    Provider = AIProviderType.GitHub,
                    ApiKey = GitHubApiKey,
                    ModelId = GitHubModelId ?? "gpt-4o"
                },
                AIProviderType.OpenAI => new AIProviderConfig
                {
                    Provider = AIProviderType.OpenAI,
                    ApiKey = OpenAIApiKey,
                    ModelId = OpenAIModelId ?? "gpt-4o"
                },
                AIProviderType.Anthropic => new AIProviderConfig
                {
                    Provider = AIProviderType.Anthropic,
                    ApiKey = AnthropicApiKey,
                    ModelId = AnthropicModelId ?? "claude-sonnet-4-5-20250929"
                },
                AIProviderType.Google => new AIProviderConfig
                {
                    Provider = AIProviderType.Google,
                    ApiKey = GoogleApiKey,
                    ModelId = GoogleModelId ?? "gemini-flash-latest"
                },
                AIProviderType.Ollama => new AIProviderConfig
                {
                    Provider = AIProviderType.Ollama,
                    Endpoint = OllamaEndpoint ?? "http://localhost:11434",
                    ModelId = OllamaModelId ?? "llama3.2",
                    DisableThinking = OllamaDisableThinking
                },
                AIProviderType.Mistral => new AIProviderConfig
                {
                    Provider = AIProviderType.Mistral,
                    ApiKey = MistralApiKey,
                    ModelId = MistralModelId ?? "mistral-large-latest"
                },
                _ => null
            };
        }

        public AIService GetAIService() => aiService;

        /// <summary>
        /// Reinitialize the AI service with current settings
        /// </summary>
        public async Task ReinitializeAsync()
        {
            await InitializeAIProviderAsync();
        }

        /// <summary>
        /// List of available providers for binding
        /// </summary>
        public List<AIProviderType> AvailableProviders => AvailableModels.GetAllProviders();

        #region Provider Selection

        public AIProviderType SelectedProvider
        {
            get
            {
                if (Enum.TryParse<AIProviderType>(Settings.Default.SelectedProvider, out var provider))
                {
                    return provider;
                }
                // Same reason as the stored default: an unreadable setting must not land on a
                // provider that was retired.
                return AIProviderType.Ollama;
            }
            set
            {
                Settings.Default.SelectedProvider = value.ToString();
                SelectedProviderInternal = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(SelectedModelId));
                ScheduleProviderInitialization();
            }
        }

        public string? SelectedModelId
        {
            get
            {
                return SelectedProvider switch
                {
                    AIProviderType.GitHub => GitHubModelId,
                    AIProviderType.OpenAI => OpenAIModelId,
                    AIProviderType.Anthropic => AnthropicModelId,
                    AIProviderType.Google => GoogleModelId,
                    AIProviderType.Ollama => OllamaModelId,
                    AIProviderType.Mistral => MistralModelId,
                    _ => "Unknown Model"
                };
            }
        }

        #endregion

        #region GitHub Models Settings

        public string? GitHubApiKey
        {
            get => Settings.Default.GitHubApiKey;
            set
            {
                Settings.Default.GitHubApiKey = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                if (SelectedProvider == AIProviderType.GitHub)
                    ScheduleProviderInitialization();
            }
        }

        public string? GitHubModelId
        {
            get
            {
                var value = Settings.Default.GitHubModelId ?? "gpt-4o";
                value = SanitizeModelId(value);
                return value;
            }
            set
            {
                Settings.Default.GitHubModelId = SanitizeModelId(value);
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                if (SelectedProvider == AIProviderType.GitHub)
                    ApplyModelSelection(AIProviderType.GitHub, Settings.Default.GitHubModelId);
            }
        }

        #endregion

        #region OpenAI Settings

        public string? OpenAIApiKey
        {
            get => Settings.Default.OpenAIApiKey;
            set
            {
                Settings.Default.OpenAIApiKey = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                if (SelectedProvider == AIProviderType.OpenAI)
                    ScheduleProviderInitialization();
            }
        }

        public string? OpenAIModelId
        {
            get => SanitizeModelId(Settings.Default.OpenAIModelId ?? "gpt-4o");
            set
            {
                Settings.Default.OpenAIModelId = SanitizeModelId(value);
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                if (SelectedProvider == AIProviderType.OpenAI)
                    ApplyModelSelection(AIProviderType.OpenAI, Settings.Default.OpenAIModelId);
            }
        }

        #endregion

        #region Anthropic Settings

        public string? AnthropicApiKey
        {
            get => Settings.Default.AnthropicApiKey;
            set
            {
                Settings.Default.AnthropicApiKey = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                if (SelectedProvider == AIProviderType.Anthropic)
                    ScheduleProviderInitialization();
            }
        }

        public string? AnthropicModelId
        {
            get => SanitizeModelId(Settings.Default.AnthropicModelId ?? "claude-3-5-sonnet-20241022");
            set
            {
                Settings.Default.AnthropicModelId = SanitizeModelId(value);
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                if (SelectedProvider == AIProviderType.Anthropic)
                    ApplyModelSelection(AIProviderType.Anthropic, Settings.Default.AnthropicModelId);
            }
        }

        #endregion

        #region Google Gemini Settings

        public string? GoogleApiKey
        {
            get => Settings.Default.GoogleApiKey;
            set
            {
                Settings.Default.GoogleApiKey = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                if (SelectedProvider == AIProviderType.Google)
                    ScheduleProviderInitialization();
            }
        }

        public string? GoogleModelId
        {
            get => SanitizeModelId(Settings.Default.GoogleModelId ?? "gemini-flash-latest");
            set
            {
                Settings.Default.GoogleModelId = SanitizeModelId(value);
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                if (SelectedProvider == AIProviderType.Google)
                    ApplyModelSelection(AIProviderType.Google, Settings.Default.GoogleModelId);
            }
        }

        #endregion

        #region Ollama Settings

        public string? OllamaEndpoint
        {
            get => Settings.Default.OllamaEndpoint ?? "http://localhost:11434";
            set
            {
                Settings.Default.OllamaEndpoint = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                if (SelectedProvider == AIProviderType.Ollama)
                    ScheduleProviderInitialization();
            }
        }

        public string? OllamaModelId
        {
            get => SanitizeModelId(Settings.Default.OllamaModelId ?? "llama3.2");
            set
            {
                Settings.Default.OllamaModelId = SanitizeModelId(value);
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                if (SelectedProvider == AIProviderType.Ollama)
                    ApplyModelSelection(AIProviderType.Ollama, Settings.Default.OllamaModelId);
            }
        }

        /// <summary>
        /// When true (default), Ollama requests skip the model's "thinking" phase.
        /// Thinking-capable models (Gemma 4, Qwen 3.x, DeepSeek) reason at length
        /// before answering by default, multiplying response times.
        /// </summary>
        public bool OllamaDisableThinking
        {
            get => Settings.Default.OllamaDisableThinking;
            set
            {
                Settings.Default.OllamaDisableThinking = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                if (SelectedProvider == AIProviderType.Ollama)
                    ScheduleProviderInitialization();
            }
        }

        #endregion

        #region Mistral Settings

        public string? MistralApiKey
        {
            get => Settings.Default.MistralApiKey;
            set
            {
                Settings.Default.MistralApiKey = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                if (SelectedProvider == AIProviderType.Mistral)
                    ScheduleProviderInitialization();
            }
        }

        public string? MistralModelId
        {
            get => SanitizeModelId(Settings.Default.MistralModelId ?? "mistral-large-latest");
            set
            {
                Settings.Default.MistralModelId = SanitizeModelId(value);
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                if (SelectedProvider == AIProviderType.Mistral)
                    ApplyModelSelection(AIProviderType.Mistral, Settings.Default.MistralModelId);
            }
        }

        #endregion

        #region MCP (Model Context Protocol) Settings

        public bool MCPEnabled
        {
            get => Settings.Default.MCPEnabled;
            set
            {
                Settings.Default.MCPEnabled = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string MCPNinaHost
        {
            get => Settings.Default.MCPNinaHost ?? "localhost";
            set
            {
                Settings.Default.MCPNinaHost = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public int MCPNinaPort
        {
            get => Settings.Default.MCPNinaPort;
            set
            {
                Settings.Default.MCPNinaPort = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// Get the MCP configuration
        /// </summary>
        public AI.MCP.MCPConfig GetMCPConfig()
        {
            return new AI.MCP.MCPConfig
            {
                Enabled = MCPEnabled,
                NinaHost = MCPNinaHost,
                NinaPort = MCPNinaPort
            };
        }

        /// <summary>
        /// External MCP Server Python executable path (e.g., python.exe or python3)
        /// </summary>
        public string ExternalMCPPythonPath
        {
            get => Settings.Default.ExternalMCPPythonPath;
            set
            {
                Settings.Default.ExternalMCPPythonPath = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// External MCP Server script path
        /// </summary>
        public string ExternalMCPScriptPath
        {
            get => Settings.Default.ExternalMCPScriptPath;
            set
            {
                Settings.Default.ExternalMCPScriptPath = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// Standard mcpServers JSON configuration for one or more external MCP servers.
        /// Example: { "mcpServers": { "weather": { "command": "python", "args": ["weather.py"] } } }
        /// </summary>
        public string ExternalMCPServersJson
        {
            get => Settings.Default.ExternalMCPServersJson;
            set
            {
                Settings.Default.ExternalMCPServersJson = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        #endregion

        #region Orchestrator (nina.autopilot) Integration — Phase 5

        public bool OrchestratorEnabled
        {
            get => Settings.Default.OrchestratorEnabled;
            set
            {
                if (Settings.Default.OrchestratorEnabled == value) return;
                Settings.Default.OrchestratorEnabled = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                OrchestratorSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string OrchestratorUrl
        {
            get => Settings.Default.OrchestratorUrl ?? "http://127.0.0.1:8765";
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? "http://127.0.0.1:8765" : value.Trim();
                if (Settings.Default.OrchestratorUrl == normalized) return;
                Settings.Default.OrchestratorUrl = normalized;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                OrchestratorSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public int OrchestratorPollIntervalSeconds
        {
            get
            {
                var v = Settings.Default.OrchestratorPollIntervalSeconds;
                return v < 1 ? 5 : v;
            }
            set
            {
                var clamped = value < 1 ? 1 : value;
                if (Settings.Default.OrchestratorPollIntervalSeconds == clamped) return;
                Settings.Default.OrchestratorPollIntervalSeconds = clamped;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                OrchestratorSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Fires when any orchestrator setting changes so AIChatVM can
        /// reconfigure (or rebuild) its OrchestratorStatusViewModel.
        /// </summary>
        public event EventHandler? OrchestratorSettingsChanged;

        #endregion

        #region Helper Methods

        private string SanitizeModelId(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "gpt-4o";
                
            // Sanitize corrupted values from ComboBoxItem binding issue
            if (value.Contains("system.windows.controls.comboboxitem:", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Split(':').LastOrDefault()?.Trim() ?? "gpt-4o";
            }
            return value;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}