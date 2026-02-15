using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NINA.Core.Utility;
using NINA.Plugin.AIAssistant.AI;
using NINA.Plugin.AIAssistant.AI.MCP;
using NINA.Plugin.AIAssistant.MCP;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.WPF.Base.ViewModel;
using NINA.Profile.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
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
            
            // Create a chat bubble icon with sparkle
            var geometry = new GeometryGroup();
            
            // Chat bubble (rounded rectangle)
            var bubble = new RectangleGeometry(new System.Windows.Rect(2, 2, 12, 10), 2, 2);
            geometry.Children.Add(bubble);
            
            // Small tail for chat bubble
            var tail = new PathGeometry();
            var figure = new PathFigure { StartPoint = new System.Windows.Point(4, 12) };
            figure.Segments.Add(new LineSegment(new System.Windows.Point(2, 14), true));
            figure.Segments.Add(new LineSegment(new System.Windows.Point(6, 12), true));
            tail.Figures.Add(figure);
            geometry.Children.Add(tail);
            
            // Sparkle/star effect (AI indicator)
            var star = new PathGeometry();
            var starFig = new PathFigure { StartPoint = new System.Windows.Point(12, 3) };
            starFig.Segments.Add(new LineSegment(new System.Windows.Point(13, 5), true));
            starFig.Segments.Add(new LineSegment(new System.Windows.Point(15, 4), true));
            starFig.Segments.Add(new LineSegment(new System.Windows.Point(13.5, 6), true));
            starFig.Segments.Add(new LineSegment(new System.Windows.Point(14, 8), true));
            starFig.Segments.Add(new LineSegment(new System.Windows.Point(12, 7), true));
            starFig.Segments.Add(new LineSegment(new System.Windows.Point(10, 8), true));
            starFig.Segments.Add(new LineSegment(new System.Windows.Point(10.5, 6), true));
            starFig.Segments.Add(new LineSegment(new System.Windows.Point(9, 4), true));
            starFig.Segments.Add(new LineSegment(new System.Windows.Point(11, 5), true));
            starFig.IsClosed = true;
            star.Figures.Add(starFig);
            geometry.Children.Add(star);
            
            geometry.Freeze();
            ImageGeometry = geometry;

            // Get AIService from plugin instance
            _aiService = AIAssistantPlugin.Instance?.GetAIService();

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
        }

        public override bool IsTool => true;
        
        public void Hide(object? o)
        {
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

        public ObservableCollection<ChatMessage> Messages { get; } = new();

        public ICommand SendMessageCommand { get; }
        public ICommand ClearChatCommand { get; }
        public ICommand StopResponseCommand { get; }

        private readonly AIService? _aiService;
        private bool _mcpInitialized = false;
        private CancellationTokenSource? _responseCancellationTokenSource;

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
            else
            {
                Logger.Info($"AIChatVM: MCP not supported for provider {_aiService?.ActiveProviderType}, only Anthropic and Google");
            }
        }

        private async Task InitializeExternalMCPAsync(AIAssistantPlugin plugin)
        {
            try
            {
                var pythonPath = plugin.ExternalMCPPythonPath;
                var scriptPath = plugin.ExternalMCPScriptPath;
                
                if (string.IsNullOrEmpty(pythonPath) || string.IsNullOrEmpty(scriptPath))
                {
                    Logger.Info("AIChatVM: External MCP not configured");
                    return;
                }
                
                var externalMCP = new ExternalMCPClient();
                var started = await externalMCP.StartServerAsync(pythonPath, scriptPath);
                
                if (started)
                {
                    Logger.Info($"AIChatVM: External MCP server started: {externalMCP.ServerName} v{externalMCP.ServerVersion}");
                    
                    // Pass to active provider
                    if (_aiService?.ActiveProviderType == AIProviderType.Anthropic)
                    {
                        var provider = _aiService.GetActiveProvider() as AnthropicProvider;
                        provider?.SetExternalMCP(externalMCP);
                    }
                    else if (_aiService?.ActiveProviderType == AIProviderType.Google)
                    {
                        var provider = _aiService.GetActiveProvider() as GoogleProvider;
                        provider?.SetExternalMCP(externalMCP);
                    }
                }
                else
                {
                    Logger.Warning("AIChatVM: Failed to start external MCP server");
                    externalMCP.Dispose();
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

            try
            {
                // Initialize MCP if needed (for Anthropic with MCP enabled)
                await InitializeMCPIfNeeded();
                
                // Build context based on whether MCP is enabled
                string? systemPrompt = null; // Let the provider use its own system prompt for MCP
                
                var mcpEnabled = plugin.MCPEnabled;
                var isMCPProvider = _aiService?.ActiveProviderType == AIProviderType.Anthropic || 
                                    _aiService?.ActiveProviderType == AIProviderType.Google;
                
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

                var response = await _aiService.QueryAsync(userMsg, systemPrompt, cancellationToken);

                Messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = response,
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
                }
                else
                {
                    statusMsg = "⚠️ Error - see message for details";
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

    public class ChatMessage : ObservableObject
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
        public bool IsAssistant => Role == "assistant";
    }
}
