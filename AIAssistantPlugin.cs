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
using System.Threading.Tasks;
using NINA.Plugin.AIAssistant.AI;

namespace NINA.Plugin.AIAssistant
{
    [Export(typeof(IPluginManifest))]
    public class AIAssistantPlugin : PluginBase, INotifyPropertyChanged
    {
        private readonly IProfileService profileService;
        private readonly AIService aiService;

        public static AIAssistantPlugin? Instance { get; private set; }

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
                    RaisePropertyChanged(nameof(IsMCPProviderSelected));
                }
            }
        }

        public bool IsGitHubSelected => SelectedProviderInternal == AIProviderType.GitHub;
        public bool IsOpenAISelected => SelectedProviderInternal == AIProviderType.OpenAI;
        public bool IsAnthropicSelected => SelectedProviderInternal == AIProviderType.Anthropic;
        public bool IsGoogleSelected => SelectedProviderInternal == AIProviderType.Google;
        public bool IsOllamaSelected => SelectedProviderInternal == AIProviderType.Ollama;
        public bool IsMCPProviderSelected => SelectedProviderInternal == AIProviderType.Anthropic || SelectedProviderInternal == AIProviderType.Google;

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
                    ModelId = GoogleModelId ?? "gemini-2.0-flash-001"
                },
                AIProviderType.Ollama => new AIProviderConfig
                {
                    Provider = AIProviderType.Ollama,
                    Endpoint = OllamaEndpoint ?? "http://localhost:11434",
                    ModelId = OllamaModelId ?? "llama3.2"
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
                return AIProviderType.GitHub;
            }
            set
            {
                Settings.Default.SelectedProvider = value.ToString();
                SelectedProviderInternal = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
                _ = InitializeAIProviderAsync();
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
                    _ = InitializeAIProviderAsync();
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
                    _ = InitializeAIProviderAsync();
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
                    _ = InitializeAIProviderAsync();
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
                    _ = InitializeAIProviderAsync();
            }
        }

        public string? GoogleModelId
        {
            get => SanitizeModelId(Settings.Default.GoogleModelId ?? "gemini-2.0-flash-001");
            set
            {
                Settings.Default.GoogleModelId = SanitizeModelId(value);
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
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
                    _ = InitializeAIProviderAsync();
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
