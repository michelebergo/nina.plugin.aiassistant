using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NINA.Core.Utility;
using NINA.Plugin.AIAssistant.AI;
using NINA.Plugin.AIAssistant.AI.MCP;
using NINA.Plugin.AIAssistant.MCP;
using NINA.Plugin.AIAssistant.Orchestrator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.WPF.Base.ViewModel;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;

// Resolve ambiguity - use NINA's RelayCommand for simple commands
using MvvmRelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;
using MvvmAsyncRelayCommand = CommunityToolkit.Mvvm.Input.AsyncRelayCommand;

namespace NINA.Plugin.AIAssistant
{
    [Export(typeof(IDockableVM))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AIChatVM : DockableVM
    {
        [ImportingConstructor]
        public AIChatVM(IProfileService profileService) : base(profileService)
        {
            Title = "AI Assistant";
            
            // The same mark as the plugin's icon, drawn as geometry for the imaging tab:
            // a speech bubble with three dots and a star beside it.
            //
            // The group fills EvenOdd, so anything drawn inside the bubble becomes a hole
            // rather than an overlay - which is how the dots are made, and which is why the
            // star has to sit clear of the bubble instead of on top of it. The previous
            // version overlapped them and punched a star-shaped hole in the bubble.
            var geometry = new GeometryGroup();
            geometry.Children.Add(new RectangleGeometry(new System.Windows.Rect(1, 3, 11, 8), 2, 2));

            var tail = new PathGeometry();
            var figure = new PathFigure { StartPoint = new System.Windows.Point(3, 10) };
            figure.Segments.Add(new LineSegment(new System.Windows.Point(2, 14), true));
            figure.Segments.Add(new LineSegment(new System.Windows.Point(6, 11), true));
            figure.IsClosed = true;
            tail.Figures.Add(figure);
            geometry.Children.Add(tail);

            // Three dots, punched out of the bubble by the fill rule.
            foreach (var x in new[] { 4.0, 6.5, 9.0 }) {
                geometry.Children.Add(new EllipseGeometry(new System.Windows.Point(x, 7), 0.9, 0.9));
            }

            // A four-pointed star, clear of the bubble so it stays a star.
            var star = new PathGeometry();
            var starFig = new PathFigure { StartPoint = new System.Windows.Point(13.6, 0.6) };
            starFig.Segments.Add(new LineSegment(new System.Windows.Point(14.3, 2.3), true));
            starFig.Segments.Add(new LineSegment(new System.Windows.Point(16.0, 3.0), true));
            starFig.Segments.Add(new LineSegment(new System.Windows.Point(14.3, 3.7), true));
            starFig.Segments.Add(new LineSegment(new System.Windows.Point(13.6, 5.4), true));
            starFig.Segments.Add(new LineSegment(new System.Windows.Point(12.9, 3.7), true));
            starFig.Segments.Add(new LineSegment(new System.Windows.Point(11.2, 3.0), true));
            starFig.Segments.Add(new LineSegment(new System.Windows.Point(12.9, 2.3), true));
            starFig.IsClosed = true;
            star.Figures.Add(starFig);
            geometry.Children.Add(star);

            geometry.Freeze();
            ImageGeometry = geometry;

            // Get AIService from plugin instance
            _aiService = AIAssistantPlugin.Instance?.GetAIService();
            
            // Re-initialize the current model from the configuration
            _currentModel = AIAssistantPlugin.Instance?.SelectedModelId ?? "Default Model";

            // Initialize commands
            SendMessageCommand = new MvvmAsyncRelayCommand(SendMessageAsync);
            ClearChatCommand = new MvvmRelayCommand(ClearChat);
            StopResponseCommand = new MvvmRelayCommand(StopResponse);

            // Add welcome message
            var mcpEnabled = AIAssistantPlugin.Instance?.MCPEnabled ?? false;
            var welcomeMsg = "Hello! I'm your AI assistant for astrophotography. Ask me anything about:\n\n" +
                         "• Equipment settings and optimization\n" +
                         "• Target selection and planning\n" +
                         "• Image processing tips\n" +
                         "• Troubleshooting issues\n\n";
            
            if (mcpEnabled)
            {
                welcomeMsg += "🤖 **MCP Control Enabled** - I can directly control your NINA equipment!\n" +
                             "Try: \"Connect to NINA\", \"Show equipment status\", \"Take a 10s exposure\"\n\n";
            }
            
            welcomeMsg += "Make sure you've configured your API key in the plugin settings!";
            
            Messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = welcomeMsg,
                Timestamp = DateTime.Now
            });

            // Subscribe to plugin settings changes to reset MCP initialization if provider/settings change
            if (AIAssistantPlugin.Instance != null)
            {
                AIAssistantPlugin.Instance.PropertyChanged += Plugin_PropertyChanged;
                AIAssistantPlugin.Instance.OrchestratorSettingsChanged += Plugin_OrchestratorSettingsChanged;
            }

            // Phase 5 — wire the orchestrator status panel
            InitializeOrchestratorVM();
        }

        #region Phase 5 — Orchestrator integration

        private OrchestratorStatusViewModel? _orchestratorVM;
        public OrchestratorStatusViewModel? OrchestratorVM
        {
            get => _orchestratorVM;
            private set => SetProperty(ref _orchestratorVM, value);
        }

        public bool OrchestratorEnabled => AIAssistantPlugin.Instance?.OrchestratorEnabled == true;

        private void InitializeOrchestratorVM()
        {
            var plugin = AIAssistantPlugin.Instance;
            if (plugin == null) return;
            if (!plugin.OrchestratorEnabled)
            {
                DisposeOrchestratorVM();
                RaisePropertyChanged(nameof(OrchestratorEnabled));
                return;
            }
            try
            {
                var vm = new OrchestratorStatusViewModel(plugin.OrchestratorUrl, plugin.OrchestratorPollIntervalSeconds);
                vm.Start();
                OrchestratorVM = vm;
                RaisePropertyChanged(nameof(OrchestratorEnabled));
            }
            catch (Exception ex)
            {
                Logger.Error($"AIChatVM: failed to initialize OrchestratorStatusViewModel: {ex.Message}");
            }
        }

        private void DisposeOrchestratorVM()
        {
            try
            {
                OrchestratorVM?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Warning($"AIChatVM: error disposing OrchestratorVM: {ex.Message}");
            }
            OrchestratorVM = null;
        }

        private void Plugin_OrchestratorSettingsChanged(object? sender, EventArgs e)
        {
            var plugin = AIAssistantPlugin.Instance;
            if (plugin == null) return;
            if (!plugin.OrchestratorEnabled)
            {
                DisposeOrchestratorVM();
                RaisePropertyChanged(nameof(OrchestratorEnabled));
                return;
            }
            if (OrchestratorVM == null)
            {
                InitializeOrchestratorVM();
            }
            else
            {
                OrchestratorVM.Reconfigure(plugin.OrchestratorUrl, plugin.OrchestratorPollIntervalSeconds);
            }
        }

        // Used internally for property-changed notifications on bool helpers.
        private void RaisePropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        #endregion

        private void Plugin_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AIAssistantPlugin.SelectedProvider) ||
                e.PropertyName == nameof(AIAssistantPlugin.MCPEnabled) ||
                e.PropertyName == "SelectedProviderInternal")
            {
                Logger.Info($"AIChatVM: Settings changed ({e.PropertyName}), resetting MCP initialization state");
                _mcpInitialized = false;

                // Update CurrentModel when provider or its model changes
                CurrentModel = AIAssistantPlugin.Instance?.SelectedModelId ?? "Default Model";

                // Update status message to prompt for re-init matching the new provider
                if (AIAssistantPlugin.Instance?.MCPEnabled == true) {
                    StatusMessage = "Ready - MCP will re-initialize on next message";
                }
            }
            else if (e.PropertyName == nameof(AIAssistantPlugin.SelectedModelId))
            {
                // Picking another model of the same provider changes neither the connection
                // nor the MCP session, so nothing is torn down here - but the panel has to
                // show the model that will actually answer the next message. It used to
                // refresh only on a provider change, which is why choosing a model looked
                // like it had not been applied.
                CurrentModel = AIAssistantPlugin.Instance?.SelectedModelId ?? "Default Model";
                Logger.Info($"AIChatVM: model changed to {CurrentModel}");
            }
        }
        
        public override bool IsTool => true;
        
        public void Hide(object? o)
        {
            _externalMcpManager?.Dispose();
            _externalMcpManager = null;
            IsClosed = true;
        }

        private string _userMessage = string.Empty;
        public string UserMessage
        {
            get => _userMessage;
            set => SetProperty(ref _userMessage, value);
        }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        private string _statusMessage = "Ready - Enter your question below";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _currentModel;
        public string CurrentModel
        {
            get => _currentModel;
            set => SetProperty(ref _currentModel, value);
        }

        private string _tokenUsage = "";
        public string TokenUsage
        {
            get => _tokenUsage;
            set => SetProperty(ref _tokenUsage, value);
        }

        private string _quotaUsage = "";
        public string QuotaUsage
        {
            get => _quotaUsage;
            set 
            {
                if (SetProperty(ref _quotaUsage, value))
                {
                    OnPropertyChanged(nameof(HasQuotaUsage));
                }
            }
        }

        public bool HasQuotaUsage => !string.IsNullOrEmpty(QuotaUsage);

        #region Context and session usage

        // What the last exchange cost, and what the conversation has cost so far. The
        // per-request figure alone never answered the question that matters during a long
        // session: how much room is left before the history has to be cut.
        private long _sessionInputTokens;
        private long _sessionOutputTokens;
        private double _sessionCost;
        private bool _sessionCostKnown;

        private double _contextPercent;
        /// <summary>How full the model's context window is, 0-100.</summary>
        public double ContextPercent
        {
            get => _contextPercent;
            private set
            {
                if (SetProperty(ref _contextPercent, value))
                {
                    OnPropertyChanged(nameof(ContextBrush));
                }
            }
        }

        private string _contextUsage = "";
        /// <summary>"12.4k / 200k · 6%", or the raw token count when the window is unknown.</summary>
        public string ContextUsage
        {
            get => _contextUsage;
            private set
            {
                if (SetProperty(ref _contextUsage, value))
                {
                    OnPropertyChanged(nameof(HasContextUsage));
                }
            }
        }

        public bool HasContextUsage => !string.IsNullOrEmpty(ContextUsage);

        private bool _hasContextWindow;
        /// <summary>Whether the bar can be drawn - false for models whose window is unknown.</summary>
        public bool HasContextWindow
        {
            get => _hasContextWindow;
            private set => SetProperty(ref _hasContextWindow, value);
        }

        private string _sessionUsage = "";
        /// <summary>Cumulative tokens and estimated cost for this conversation.</summary>
        public string SessionUsage
        {
            get => _sessionUsage;
            private set
            {
                if (SetProperty(ref _sessionUsage, value))
                {
                    OnPropertyChanged(nameof(HasSessionUsage));
                }
            }
        }

        public bool HasSessionUsage => !string.IsNullOrEmpty(SessionUsage);

        /// <summary>
        /// The bar turns amber past two thirds and red past ninety percent: by then the
        /// oldest turns are about to be dropped, and that is worth noticing before the
        /// assistant starts forgetting what was agreed earlier in the night.
        /// </summary>
        public string ContextBrush =>
            ContextPercent >= 90 ? "#D9534F" :
            ContextPercent >= 66 ? "Goldenrod" :
            "#5CB85C";

        /// <summary>
        /// The provider's rate-limit budget, in the same compact units as everything else.
        /// Raw counts like "11971000/12000000 tokens" are unreadable at a glance and were
        /// the loudest thing in the header; "12.0M left of 12.0M" says the same in a form
        /// the eye can take in.
        /// </summary>
        private static string FormatQuota(string? requestsLeft, string? requestsLimit, string? tokensLeft, string? tokensLimit)
        {
            string Compact(string? raw) =>
                long.TryParse(raw, out var value) ? ModelLimits.FormatTokens(value) : (raw ?? "?");

            var requests = string.IsNullOrEmpty(requestsLimit)
                ? $"{Compact(requestsLeft)} req"
                : $"{Compact(requestsLeft)}/{Compact(requestsLimit)} req";

            var tokens = string.IsNullOrEmpty(tokensLimit)
                ? $"{Compact(tokensLeft)} tok"
                : $"{Compact(tokensLeft)}/{Compact(tokensLimit)} tok";

            return $"API left {requests} · {tokens}";
        }

        /// <summary>
        /// Folds one exchange into the readout. The input tokens of a request are the whole
        /// prompt - system, tools and history included - so they are the size of the
        /// context at that moment, not just of the question that was typed.
        /// </summary>
        private void UpdateUsage(string? modelId, long inputTokens, long outputTokens)
        {
            _sessionInputTokens += inputTokens;
            _sessionOutputTokens += outputTokens;

            var window = ModelLimits.ContextWindowFor(modelId);
            HasContextWindow = window.HasValue && inputTokens > 0;

            if (window.HasValue && window.Value > 0)
            {
                ContextPercent = Math.Min(100.0, inputTokens * 100.0 / window.Value);
                var remaining = Math.Max(0, window.Value - inputTokens);
                // The bar carries the percentage, so the text carries the absolute numbers:
                // how much of the window is in use, and how much room is left before the
                // conversation has to be trimmed.
                ContextUsage = $"{ModelLimits.FormatTokens(inputTokens)} / {ModelLimits.FormatTokens(window.Value)} · {ModelLimits.FormatTokens(remaining)} left";
            }
            else
            {
                ContextPercent = 0;
                ContextUsage = inputTokens > 0 ? $"{ModelLimits.FormatTokens(inputTokens)} context" : "";
            }

            var cost = ModelLimits.EstimateCost(modelId, inputTokens, outputTokens);
            if (cost.HasValue)
            {
                _sessionCost += cost.Value;
                _sessionCostKnown = true;
            }

            var totals = $"↑{ModelLimits.FormatTokens(_sessionInputTokens)} ↓{ModelLimits.FormatTokens(_sessionOutputTokens)}";
            SessionUsage = _sessionCostKnown ? $"{totals} · ~${_sessionCost:0.000}" : totals;
        }

        #endregion

        private bool _isMcpSupportedModel = false;
        public bool IsMcpSupportedModel
        {
            get => _isMcpSupportedModel;
            set => SetProperty(ref _isMcpSupportedModel, value);
        }

        public ObservableCollection<ChatMessage> Messages { get; } = new();

        public ICommand SendMessageCommand { get; }
        public ICommand ClearChatCommand { get; }
        public ICommand StopResponseCommand { get; }

        private readonly AIService? _aiService;
        private bool _mcpInitialized = false;
        private ExternalMCPManager? _externalMcpManager;
        private CancellationTokenSource? _responseCancellationTokenSource;

        /// <summary>
        /// Ends a trace: it is dropped when no tool ran (the assistant simply answered, and
        /// an empty "worked on it" entry is noise), otherwise collapsed to a headline that
        /// says how much work the answer took.
        /// </summary>
        private void CollapseActivity(ChatMessage activity, bool shown)
        {
            if (!shown)
            {
                return;
            }

            if (activity.ToolCallCount == 0)
            {
                Messages.Remove(activity);
                return;
            }

            var tools = activity.ToolCallCount == 1 ? "1 tool call" : $"{activity.ToolCallCount} tool calls";
            activity.Summary = $"⚙ {tools}";
        }

        /// <summary>
        /// Progress arrives from the provider's worker thread, while the trace lines land
        /// in a bound collection, so every update is marshalled to the UI thread.
        /// </summary>
        private static void RunOnUIThread(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.BeginInvoke(action);
            }
        }

        private async Task InitializeMCPIfNeeded()
        {
            if (_mcpInitialized) 
            {
                Logger.Debug("AIChatVM: MCP already initialized, skipping");
                return;
            }
            
            var plugin = AIAssistantPlugin.Instance;
            if (plugin == null)
            {
                Logger.Warning("AIChatVM: Plugin instance is null, cannot initialize MCP");
                return;
            }
            
            Logger.Info($"AIChatVM: MCP Enabled setting: {plugin.MCPEnabled}");
            Logger.Info($"AIChatVM: Selected Provider: {plugin.SelectedProvider}");
            Logger.Info($"AIChatVM: AIService ActiveProviderType: {_aiService?.ActiveProviderType}");
            
            if (!plugin.MCPEnabled)
            {
                Logger.Info("AIChatVM: MCP is disabled in settings");
                return;
            }
            
            // Initialize external MCP server if configured
            await InitializeExternalMCPAsync(plugin);
            
            // Enable MCP for Anthropic and Google providers
            if (_aiService?.ActiveProviderType == AIProviderType.Anthropic)
            {
                var provider = _aiService.GetActiveProvider() as AnthropicProvider;
                if (provider != null)
                {
                    var mcpConfig = plugin.GetMCPConfig();
                    Logger.Info($"AIChatVM: Initializing MCP for Anthropic - Host: {mcpConfig.NinaHost}, Port: {mcpConfig.NinaPort}, Enabled: {mcpConfig.Enabled}");
                    
                    var success = await provider.EnableMCPAsync(mcpConfig);
                    _mcpInitialized = success;
                    
                    if (success)
                    {
                        Logger.Info("AIChatVM: MCP initialized successfully for Anthropic provider");
                        StatusMessage = "🤖 MCP Connected (Claude)";
                    }
                    else
                    {
                        Logger.Warning("AIChatVM: MCP initialization failed - check NINA Advanced API connection");
                        StatusMessage = "⚠️ MCP connection failed";
                    }
                }
                else
                {
                    Logger.Warning("AIChatVM: Could not cast active provider to AnthropicProvider");
                }
            }
            else if (_aiService?.ActiveProviderType == AIProviderType.Google)
            {
                var provider = _aiService.GetActiveProvider() as GoogleProvider;
                if (provider != null)
                {
                    var mcpConfig = plugin.GetMCPConfig();
                    Logger.Info($"AIChatVM: Initializing MCP for Google Gemini - Host: {mcpConfig.NinaHost}, Port: {mcpConfig.NinaPort}, Enabled: {mcpConfig.Enabled}");
                    
                    var success = await provider.EnableMCPAsync(mcpConfig);
                    _mcpInitialized = success;
                    
                    if (success)
                    {
                        Logger.Info("AIChatVM: MCP initialized successfully for Google provider");
                        StatusMessage = "🤖 MCP Connected (Gemini)";
                    }
                    else
                    {
                        Logger.Warning("AIChatVM: MCP initialization failed - check NINA Advanced API connection");
                        StatusMessage = "⚠️ MCP connection failed";
                    }
                }
                else
                {
                    Logger.Warning("AIChatVM: Could not cast active provider to GoogleProvider");
                }
            }
            else if (_aiService?.ActiveProviderType == AIProviderType.Ollama)
            {
                var provider = _aiService.GetActiveProvider() as OllamaProvider;
                if (provider != null)
                {
                    var mcpConfig = plugin.GetMCPConfig();
                    Logger.Info($"AIChatVM: Initializing MCP for Ollama - Host: {mcpConfig.NinaHost}, Port: {mcpConfig.NinaPort}, Enabled: {mcpConfig.Enabled}");
                    
                    var success = await provider.EnableMCPAsync(mcpConfig);
                    _mcpInitialized = success;
                    
                    if (success)
                    {
                        Logger.Info("AIChatVM: MCP initialized successfully for Ollama provider");
                        StatusMessage = "🤖 MCP Connected (Ollama)";
                    }
                    else
                    {
                        Logger.Warning("AIChatVM: MCP initialization failed - check NINA Advanced API connection");
                        StatusMessage = "⚠️ MCP connection failed";
                    }
                }
                else
                {
                    Logger.Warning("AIChatVM: Could not cast active provider to OllamaProvider");
                }
            }
            else
            {
                Logger.Info($"AIChatVM: MCP not supported for provider {_aiService?.ActiveProviderType}, only Anthropic, Google, and Ollama");
            }
        }

        private async Task InitializeExternalMCPAsync(AIAssistantPlugin plugin)
        {
            try
            {
                // Parse the standard mcpServers JSON configuration (supports multiple servers).
                var configs = ExternalMCPConfigParser.Parse(plugin.ExternalMCPServersJson);

                // Backward-compatibility migration: if no JSON config but legacy single-server
                // paths are set, synthesize one server config.
                if (configs.Count == 0 && !string.IsNullOrWhiteSpace(plugin.ExternalMCPScriptPath))
                {
                    configs.Add(new ExternalMCPServerConfig
                    {
                        Name = "default",
                        Command = string.IsNullOrWhiteSpace(plugin.ExternalMCPPythonPath) ? "python" : plugin.ExternalMCPPythonPath,
                        Args = new System.Collections.Generic.List<string> { plugin.ExternalMCPScriptPath }
                    });
                }

                if (configs.Count == 0)
                {
                    Logger.Info("AIChatVM: External MCP not configured");
                    return;
                }

                // Dispose any previous manager before starting a new one.
                _externalMcpManager?.Dispose();
                _externalMcpManager = null;

                var manager = new ExternalMCPManager();
                var anyStarted = await manager.StartAllAsync(configs);

                if (anyStarted)
                {
                    _externalMcpManager = manager;
                    Logger.Info($"AIChatVM: External MCP started: {manager.ConnectedCount} server(s) - {manager.ServerName}");

                    // Pass to active provider
                    if (_aiService?.ActiveProviderType == AIProviderType.Anthropic)
                    {
                        var provider = _aiService.GetActiveProvider() as AnthropicProvider;
                        provider?.SetExternalMCP(manager);
                    }
                    else if (_aiService?.ActiveProviderType == AIProviderType.Google)
                    {
                        var provider = _aiService.GetActiveProvider() as GoogleProvider;
                        provider?.SetExternalMCP(manager);
                    }
                    else if (_aiService?.ActiveProviderType == AIProviderType.Ollama)
                    {
                        var provider = _aiService.GetActiveProvider() as OllamaProvider;
                        provider?.SetExternalMCP(manager);
                    }
                }
                else
                {
                    Logger.Warning("AIChatVM: No external MCP servers started");
                    manager.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"AIChatVM: External MCP initialization error: {ex.Message}");
            }
        }

        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(UserMessage))
                return;

            // Check if the current provider has an API key configured
            var plugin = AIAssistantPlugin.Instance;
            if (plugin == null)
            {
                StatusMessage = "⚠️ Plugin not initialized";
                return;
            }

            // Validate API key based on selected provider
            var provider = plugin.SelectedProvider;
            bool hasValidKey = provider switch
            {
                AIProviderType.GitHub => !string.IsNullOrEmpty(plugin.GitHubApiKey),
                AIProviderType.OpenAI => !string.IsNullOrEmpty(plugin.OpenAIApiKey),
                AIProviderType.Anthropic => !string.IsNullOrEmpty(plugin.AnthropicApiKey),
                AIProviderType.Google => !string.IsNullOrEmpty(plugin.GoogleApiKey),
                AIProviderType.Ollama => true, // Ollama doesn't need API key
                AIProviderType.Mistral => !string.IsNullOrEmpty(plugin.MistralApiKey),
                _ => false
            };

            if (!hasValidKey)
            {
                StatusMessage = $"⚠️ Please configure your {provider} API key in Options → Plugins";
                return;
            }

            if (_aiService == null)
            {
                StatusMessage = "⚠️ AI Service not initialized";
                return;
            }

            var userMsg = UserMessage;
            UserMessage = string.Empty;

            // Add user message to chat
            Messages.Add(new ChatMessage
            {
                Role = "user",
                Content = userMsg,
                Timestamp = DateTime.Now
            });

            IsProcessing = true;
            StatusMessage = "🤔 Thinking...";

            // Create new cancellation token source for this response
            _responseCancellationTokenSource?.Cancel();
            _responseCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _responseCancellationTokenSource.Token;

            AIResponse? response = null;

            // The trace of what the assistant does to answer. Declared out here so the
            // finally can close it however the request ends. It only joins the conversation
            // once there is something to show, so a plain question leaves no trace behind.
            var activity = new ChatMessage
            {
                Role = "activity",
                Timestamp = DateTime.Now,
                // Collapsed from the start: while the request runs the headline shows the
                // step in progress and stays one line tall. A trace that expanded as it
                // grew pushed the conversation off the screen on any answer that used
                // several tools - the work was on show, the discussion was not.
                IsExpanded = false,
                Summary = "Working..."
            };
            var activityShown = false;

            try
            {
                // Initialize MCP if needed (for Anthropic with MCP enabled)
                await InitializeMCPIfNeeded();
                
                // Build context based on whether MCP is enabled
                string? systemPrompt = null; // Let the provider use its own system prompt for MCP
                
                var mcpEnabled = plugin.MCPEnabled;
                var isMCPProvider = _aiService?.ActiveProviderType == AIProviderType.Anthropic || 
                                    _aiService?.ActiveProviderType == AIProviderType.Google ||
                                    _aiService?.ActiveProviderType == AIProviderType.Ollama;
                
                // Let MCP-capable providers (Anthropic, Google) use their own MCP system prompt
                if (!mcpEnabled || !isMCPProvider)
                {
                    systemPrompt = @"You are an expert astrophotography assistant integrated into N.I.N.A. (Nighttime Imaging 'N' Astronomy) software version 3.x.

IMPORTANT RULES:
- Only answer questions related to astrophotography, astronomy, N.I.N.A. software, and imaging equipment.
- If you don't know something, say so. NEVER fabricate equipment specs, camera sensor data, or telescope specifications.
- Do NOT invent features or settings that don't exist in N.I.N.A.
- When discussing specific equipment, only state facts you are certain about.

Your expertise includes:
- Camera setup: gain, offset, cooling, binning, ROI for ZWO, QHY, Atik, and other astro cameras
- Mount control: alignment, tracking, meridian flips, park/unpark, goto for EQ and Alt-Az mounts
- Focuser operations: autofocus routines, HFR analysis, temperature compensation, Bahtinov mask focusing
- Filter wheels: LRGB, narrowband (Ha, OIII, SII) filter selection and sequencing
- Guiding: PHD2 integration, guide star selection, calibration, dithering strategies
- Platesolving: blind and near solves, center/rotate accuracy, Astap/ANSVR/PlateSolve2
- Imaging session planning: target selection, exposure times, filter sequences, mosaic planning
- Image quality: HFR interpretation, star shapes, trailing, vignetting, amp glow, walking noise
- Flat, dark, bias frame acquisition and calibration strategies

Keep responses concise but accurate. Use proper astrophotography terminology.";
                }

                // Build conversation history (multi-turn context): include the FULL prior conversation,
                // skipping only the welcome message (first) and the current user message (last).
                var history = new List<AIChatTurn>();
                if (Messages.Count > 2)
                {
                    history = Messages
                        .Skip(1)                          // skip the welcome message
                        .Take(Messages.Count - 2)         // exclude the current user message (last)
                        .Where(m => !m.IsError && (m.Role == "user" || m.Role == "assistant"))
                        .Select(m => new AIChatTurn { Role = m.Role, Content = m.Content })
                        .ToList();
                }

                var request = new AIRequest
                {
                    Prompt = userMsg,
                    SystemPrompt = systemPrompt,
                    MaxTokens = 1024,
                    Temperature = 0.7,
                    History = history,
                    ProgressCallback = (msg) =>
                    {
                        StatusMessage = msg;
                        RunOnUIThread(() =>
                        {
                            if (!activityShown)
                            {
                                activityShown = true;
                                Messages.Add(activity);
                            }
                            if (msg.Contains("Calling")) { activity.ToolCallCount++; }

                            // The headline is the step happening now; the full sequence
                            // accumulates behind it for whoever wants to open it afterwards.
                            activity.Summary = ChatMessage.AsHeadline(msg);
                            activity.AppendActivity(msg);
                        });
                    }
                };

                try
                {
                    response = await _aiService.SendRequestAsync(request, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Catch internal provider errors that don't return a Success=false AIResponse
                    Logger.Error($"AI Service direct failure: {ex.Message}");
                    throw; // Re-throw to be handled by the main catch block
                }

                // The work is finished: the trace collapses to its headline so the answer
                // stays the thing you read, with the detail one click away when a tool did
                // something surprising.
                RunOnUIThread(() => CollapseActivity(activity, activityShown));

                if (response == null || !response.Success)
                {
                    throw new Exception(response?.Error ?? "Unknown error");
                }

                // Update UI with model and token information
                CurrentModel = response.ModelUsed ?? _aiService.ActiveProviderName ?? "Unknown Model";
                
                // Format detailed token usage if available
                if (response.Metadata != null && response.Metadata.ContainsKey("input_tokens"))
                {
                    var inTok = response.Metadata["input_tokens"];
                    var outTok = response.Metadata["output_tokens"];
                    TokenUsage = $"{inTok} in | {outTok} out ({response.TokensUsed ?? 0} total)";

                    UpdateUsage(response.ModelUsed ?? CurrentModel,
                                Convert.ToInt64(inTok ?? 0),
                                Convert.ToInt64(outTok ?? 0));
                }
                else
                {
                    TokenUsage = response.TokensUsed.HasValue ? $"{response.TokensUsed} tokens" : "Tokens: N/A";

                    // Providers that report only a total still contribute to the session
                    // count; without a split, the context figure stays unknown rather than
                    // being invented from half the data.
                    UpdateUsage(response.ModelUsed ?? CurrentModel, 0, response.TokensUsed ?? 0);
                }
                
                IsMcpSupportedModel = isMCPProvider;

                // Extract and format quota usage if available
                if (response?.Metadata != null && response.Metadata.ContainsKey("requests_remaining"))
                {
                    var reqRem = response.Metadata.ContainsKey("requests_remaining") ? response.Metadata["requests_remaining"]?.ToString() : null;
                    var reqLim = response.Metadata.ContainsKey("requests_limit") ? response.Metadata["requests_limit"]?.ToString() : null;
                    var tokRem = response.Metadata.ContainsKey("tokens_remaining") ? response.Metadata["tokens_remaining"]?.ToString() : null;
                    var tokLim = response.Metadata.ContainsKey("tokens_limit") ? response.Metadata["tokens_limit"]?.ToString() : null;
                    
                    if (!string.IsNullOrEmpty(reqRem) && !string.IsNullOrEmpty(tokRem))
                    {
                        QuotaUsage = FormatQuota(reqRem, reqLim, tokRem, tokLim);
                    }
                }
                else
                {
                    QuotaUsage = "";
                }

                Messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = response.Content ?? "No response received",
                    Timestamp = DateTime.Now
                });

                StatusMessage = "✓ Ready";
            }            catch (OperationCanceledException)
            {
                // User stopped the response
                Messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = "[Response stopped by user]",
                    Timestamp = DateTime.Now,
                    IsError = false
                });
                StatusMessage = "Stopped";
            }            catch (Exception ex)
            {
                Logger.Error($"AI Query failed: {ex.Message}");
                var errorMsg = ex.Message;
                string statusMsg;
                
                if (errorMsg.Contains("Model not found"))
                {
                    statusMsg = "⚠️ Error - model not found";
                }
                else if (errorMsg.Contains("authentication") || errorMsg.Contains("Invalid API key") || errorMsg.Contains("Unauthorized"))
                {
                    statusMsg = "⚠️ Error - check your API token";
                }
                else if (errorMsg.Contains("Rate limit") || errorMsg.Contains("rate_limit"))
                {
                    statusMsg = "⚠️ Rate limited - try again shortly";
                    
                    // Specific handling for Anthropic rate limit resetting
                    if (ex.Data.Contains("requests_reset")) {
                         errorMsg += $"\nRequests reset in: {ex.Data["requests_reset"]}";
                    }
                    if (ex.Data.Contains("tokens_reset")) {
                         errorMsg += $"\nTokens reset in: {ex.Data["tokens_reset"]}";
                    }
                    if (ex.Data.Contains("retry_after")) {
                         errorMsg += $"\nRetry after: {ex.Data["retry_after"]}s";
                    }
                }
                else
                {
                    statusMsg = "⚠️ Error - see message for details";
                }
                
                // Try to get quota info even on error
                if (_aiService?.GetActiveProvider() is AnthropicProvider anthropic && response?.Metadata != null)
                {
                     var reqRem = response.Metadata.ContainsKey("requests_remaining") ? response.Metadata["requests_remaining"]?.ToString() : null;
                     var tokRem = response.Metadata.ContainsKey("tokens_remaining") ? response.Metadata["tokens_remaining"]?.ToString() : null;
                     if (!string.IsNullOrEmpty(reqRem)) QuotaUsage = FormatQuota(reqRem, null, tokRem, null);
                }

                Messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = $"Sorry, I encountered an error: {errorMsg}",
                    Timestamp = DateTime.Now,
                    IsError = true
                });
                StatusMessage = statusMsg;
            }
            finally
            {
                IsProcessing = false;

                // A trace whose headline is still a live step means the request ended badly.
                // It keeps its one line and says so; the detail is a click away, like any
                // other trace, rather than unfolding over the conversation.
                if (activityShown && !activity.Summary.StartsWith("⚙"))
                {
                    activity.Summary = activity.ToolCallCount > 0
                        ? $"⚙ {activity.ToolCallCount} tool call(s) — interrupted"
                        : "⚙ interrupted";
                }
            }
        }

        private void ClearChat()
        {
            Messages.Clear();
            Messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = "Chat cleared. How can I help you?",
                Timestamp = DateTime.Now
            });
            StatusMessage = "Ready";
        }

        private void StopResponse()
        {
            _responseCancellationTokenSource?.Cancel();
            StatusMessage = "Stopping...";
        }

        public void Dispose() { }
    }

    public partial class ChatMessage : ObservableObject
    {
        private string _role = string.Empty;
        public string Role
        {
            get => _role;
            set
            {
                SetProperty(ref _role, value);
                OnPropertyChanged(nameof(IsUser));
                OnPropertyChanged(nameof(IsAssistant));
            }
        }

        private string _content = string.Empty;
        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        private DateTime _timestamp;
        public DateTime Timestamp
        {
            get => _timestamp;
            set => SetProperty(ref _timestamp, value);
        }

        private bool _isError;
        public bool IsError
        {
            get => _isError;
            set => SetProperty(ref _isError, value);
        }

        public bool IsUser => Role == "user";
        public bool IsAssistant => Role == "assistant" && !IsActivity;

        /// <summary>
        /// An activity entry: what the assistant did to answer, rather than the answer.
        /// It is a chat entry and not a status line because the work is part of the
        /// conversation - which tools ran, on what, and whether they succeeded - and a
        /// status line that overwrites itself keeps none of it.
        /// </summary>
        public bool IsActivity => Role == "activity";

        private bool _isExpanded;
        /// <summary>Expanded while the request runs, collapsed to the summary afterwards.</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        private string _summary = string.Empty;
        /// <summary>The one-line headline shown when the trace is collapsed.</summary>
        public string Summary
        {
            get => _summary;
            set => SetProperty(ref _summary, value);
        }

        /// <summary>How many tool calls this trace recorded, for the summary line.</summary>
        public int ToolCallCount { get; set; }

        /// <summary>Appends one live line to the trace.</summary>
        public void AppendActivity(string line)
        {
            Content = string.IsNullOrEmpty(Content) ? line : Content + "\n" + line;
        }

        /// <summary>
        /// One progress event rendered as a single-line headline. Long tool arguments are
        /// cut here rather than at the source, because the full text is still worth having
        /// in the expanded trace.
        /// </summary>
        public static string AsHeadline(string message)
        {
            var single = (message ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            return single.Length <= 64 ? single : single.Substring(0, 64) + "...";
        }
    }
}
