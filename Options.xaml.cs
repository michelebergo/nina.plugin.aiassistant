using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Azure.AI.Inference;
using Azure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NINA.Plugin.AIAssistant.AI;

namespace NINA.Plugin.AIAssistant
{
    [Export(typeof(ResourceDictionary))]
    public partial class Options : ResourceDictionary
    {
        public Options()
        {
            InitializeComponent();
        }

        #region Provider Selection

        private void ProviderSelector_Loaded(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.DataContext is AIAssistantPlugin plugin)
            {
                // Set the selected item based on the saved provider
                var savedProvider = plugin.SelectedProvider.ToString();
                foreach (ComboBoxItem item in comboBox.Items)
                {
                    if (item.Tag?.ToString() == savedProvider)
                    {
                        comboBox.SelectedItem = item;
                        break;
                    }
                }
                
                // Update status text
                UpdateProviderStatus(comboBox, plugin);
            }
        }

        private async void ProviderSelector_Changed(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.SelectedItem is ComboBoxItem selectedItem && 
                comboBox.DataContext is AIAssistantPlugin plugin)
            {
                var providerTag = selectedItem.Tag?.ToString();
                if (!string.IsNullOrEmpty(providerTag) && 
                    Enum.TryParse<AIProviderType>(providerTag, out var providerType))
                {
                    plugin.SelectedProvider = providerType;
                    UpdateProviderStatus(comboBox, plugin);
                    
                    // Load available models for the selected provider
                    await LoadModelsForProvider(providerType, plugin);
                }
            }
        }

        private void UpdateProviderStatus(ComboBox comboBox, AIAssistantPlugin plugin)
        {
            var statusText = FindTextBlock("ProviderStatusText", comboBox);
            if (statusText == null) return;

            var provider = plugin.SelectedProvider;
            string status = provider switch
            {
                AIProviderType.Anthropic when plugin.MCPEnabled => "✓ MCP-enabled for NINA control",
                AIProviderType.Anthropic => "Claude AI (enable MCP for equipment control)",
                AIProviderType.Google when plugin.MCPEnabled => "✓ MCP-enabled for NINA control",
                AIProviderType.Google => "Google Gemini (enable MCP for equipment control)",
                AIProviderType.GitHub => "Using GitHub-hosted models",
                AIProviderType.OpenAI => "Using OpenAI API",
                AIProviderType.Ollama => "Local AI models",
                _ => ""
            };
            statusText.Text = status;
        }

        #endregion

        #region Model ComboBox Handlers

        private void ModelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // When user selects from dropdown, sync the custom textbox
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
            {
                var modelId = item.Content?.ToString();
                if (!string.IsNullOrWhiteSpace(modelId))
                {
                    // Find corresponding custom textbox and update it
                    var customBox = comboBox.Name switch
                    {
                        "GitHubModelComboBox" => FindControl<TextBox>("GitHubCustomModelBox"),
                        "OpenAIModelComboBox" => FindControl<TextBox>("OpenAICustomModelBox"),
                        "AnthropicModelComboBox" => FindControl<TextBox>("AnthropicCustomModelBox"),
                        "GoogleModelComboBox" => FindControl<TextBox>("GoogleCustomModelBox"),
                        _ => null
                    };
                    
                    if (customBox != null)
                    {
                        customBox.Text = modelId;
                    }
                }
            }
        }

        private async void GitHubModel_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.DataContext is AIAssistantPlugin plugin)
            {
                await LoadModelsForProvider(AIProviderType.GitHub, plugin);
            }
        }

        private async void OpenAIModel_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.DataContext is AIAssistantPlugin plugin)
            {
                await LoadModelsForProvider(AIProviderType.OpenAI, plugin);
            }
        }

        private async void AnthropicModel_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.DataContext is AIAssistantPlugin plugin)
            {
                await LoadModelsForProvider(AIProviderType.Anthropic, plugin);
            }
        }

        private async void GoogleModel_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.DataContext is AIAssistantPlugin plugin)
            {
                await LoadModelsForProvider(AIProviderType.Google, plugin);
            }
        }

        private async void OllamaModel_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.DataContext is AIAssistantPlugin plugin)
            {
                await LoadModelsForProvider(AIProviderType.Ollama, plugin);
            }
        }

        #endregion

        #region GitHub Token Handlers

        private async void GitHubToken_Changed(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox?.DataContext is AIAssistantPlugin plugin)
            {
                plugin.GitHubApiKey = passwordBox.Password;
                // Auto-reload models when API key changes
                if (!string.IsNullOrWhiteSpace(plugin.GitHubApiKey))
                {
                    await LoadModelsForProvider(AIProviderType.GitHub, plugin);
                }
            }
            ClearTestResult("GitHubTestResult", sender);
        }

        private void GitHubToken_Loaded(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox?.DataContext is AIAssistantPlugin plugin && !string.IsNullOrEmpty(plugin.GitHubApiKey))
            {
                passwordBox.Password = plugin.GitHubApiKey;
            }
        }

        private void GetGitHubToken_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/settings/tokens/new",
                UseShellExecute = true
            });
        }

        private async void TestGitHubKey_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is not AIAssistantPlugin plugin) return;

            var resultTextBlock = FindTextBlock("GitHubTestResult", button);
            if (resultTextBlock == null) return;

            if (string.IsNullOrWhiteSpace(plugin.GitHubApiKey))
            {
                ShowResult(resultTextBlock, "⚠️ Please enter an API token first", Colors.Orange);
                return;
            }

            button.IsEnabled = false;
            ShowResult(resultTextBlock, "🔄 Testing API key...", Colors.White);

            try
            {
                var endpoint = new Uri("https://models.inference.ai.azure.com");
                var credential = new AzureKeyCredential(plugin.GitHubApiKey);
                var client = new ChatCompletionsClient(endpoint, credential);

                var options = new ChatCompletionsOptions
                {
                    Model = plugin.GitHubModelId ?? "gpt-4o",
                    Messages = { new ChatRequestUserMessage("Say 'OK'") }
                };
                // GitHub Models uses max_completion_tokens instead of max_tokens
                options.AdditionalProperties["max_completion_tokens"] = BinaryData.FromObjectAsJson(5);

                var response = await client.CompleteAsync(options);

                ShowResult(resultTextBlock, $"✅ GitHub API key is valid!", Colors.LightGreen);
            }
            catch (Exception ex)
            {
                HandleApiError(resultTextBlock, ex);
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        #endregion

        #region OpenAI Token Handlers

        private async void OpenAIToken_Changed(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox?.DataContext is AIAssistantPlugin plugin)
            {
                plugin.OpenAIApiKey = passwordBox.Password;
                // Auto-reload models when API key changes
                if (!string.IsNullOrWhiteSpace(plugin.OpenAIApiKey))
                {
                    await LoadModelsForProvider(AIProviderType.OpenAI, plugin);
                }
            }
            ClearTestResult("OpenAITestResult", sender);
        }

        private void OpenAIToken_Loaded(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox?.DataContext is AIAssistantPlugin plugin && !string.IsNullOrEmpty(plugin.OpenAIApiKey))
            {
                passwordBox.Password = plugin.OpenAIApiKey;
            }
        }

        private void GetOpenAIKey_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://platform.openai.com/api-keys",
                UseShellExecute = true
            });
        }

        private async void TestOpenAIKey_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is not AIAssistantPlugin plugin) return;

            var resultTextBlock = FindTextBlock("OpenAITestResult", button);
            if (resultTextBlock == null) return;

            if (string.IsNullOrWhiteSpace(plugin.OpenAIApiKey))
            {
                ShowResult(resultTextBlock, "⚠️ Please enter an API key first", Colors.Orange);
                return;
            }

            button.IsEnabled = false;
            ShowResult(resultTextBlock, "🔄 Testing API key...", Colors.White);

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", plugin.OpenAIApiKey);
                
                var response = await client.GetAsync("https://api.openai.com/v1/models");
                
                if (response.IsSuccessStatusCode)
                {
                    ShowResult(resultTextBlock, "✅ OpenAI API key is valid!", Colors.LightGreen);
                }
                else
                {
                    var content = await response.Content.ReadAsStringAsync();
                    ShowResult(resultTextBlock, $"❌ Invalid API key: {response.StatusCode}", Colors.Salmon);
                }
            }
            catch (Exception ex)
            {
                HandleApiError(resultTextBlock, ex);
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        #endregion

        #region Anthropic Token Handlers

        private async void AnthropicToken_Changed(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox?.DataContext is AIAssistantPlugin plugin)
            {
                plugin.AnthropicApiKey = passwordBox.Password;
                // Auto-reload models when API key changes
                if (!string.IsNullOrWhiteSpace(plugin.AnthropicApiKey))
                {
                    await LoadModelsForProvider(AIProviderType.Anthropic, plugin);
                }
            }
            ClearTestResult("AnthropicTestResult", sender);
        }

        private void AnthropicToken_Loaded(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox?.DataContext is AIAssistantPlugin plugin && !string.IsNullOrEmpty(plugin.AnthropicApiKey))
            {
                passwordBox.Password = plugin.AnthropicApiKey;
            }
        }

        private void GetAnthropicKey_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://console.anthropic.com/settings/keys",
                UseShellExecute = true
            });
        }

        private async void TestAnthropicKey_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is not AIAssistantPlugin plugin) return;

            var resultTextBlock = FindTextBlock("AnthropicTestResult", button);
            if (resultTextBlock == null) return;

            if (string.IsNullOrWhiteSpace(plugin.AnthropicApiKey))
            {
                ShowResult(resultTextBlock, "⚠️ Please enter an API key first", Colors.Orange);
                return;
            }

            button.IsEnabled = false;
            ShowResult(resultTextBlock, "🔄 Testing API key...", Colors.White);

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("x-api-key", plugin.AnthropicApiKey);
                client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                var requestBody = new
                {
                    model = "claude-3-5-haiku-20241022",
                    max_tokens = 5,
                    messages = new[] { new { role = "user", content = "Say OK" } }
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("https://api.anthropic.com/v1/messages", content);

                if (response.IsSuccessStatusCode)
                {
                    ShowResult(resultTextBlock, "✅ Anthropic API key is valid!", Colors.LightGreen);
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    ShowResult(resultTextBlock, $"❌ Invalid API key: {response.StatusCode}", Colors.Salmon);
                }
            }
            catch (Exception ex)
            {
                HandleApiError(resultTextBlock, ex);
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        #endregion

        #region Google Token Handlers

        private async void GoogleToken_Changed(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox?.DataContext is AIAssistantPlugin plugin)
            {
                plugin.GoogleApiKey = passwordBox.Password;
                // Auto-reload models when API key changes
                if (!string.IsNullOrWhiteSpace(plugin.GoogleApiKey))
                {
                    await LoadModelsForProvider(AIProviderType.Google, plugin);
                }
            }
            ClearTestResult("GoogleTestResult", sender);
        }

        private void GoogleToken_Loaded(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox?.DataContext is AIAssistantPlugin plugin && !string.IsNullOrEmpty(plugin.GoogleApiKey))
            {
                passwordBox.Password = plugin.GoogleApiKey;
            }
        }

        private void GetGoogleKey_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://aistudio.google.com/app/apikey",
                UseShellExecute = true
            });
        }

        private async void TestGoogleKey_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is not AIAssistantPlugin plugin) return;

            var resultTextBlock = FindTextBlock("GoogleTestResult", button);
            if (resultTextBlock == null) return;

            if (string.IsNullOrWhiteSpace(plugin.GoogleApiKey))
            {
                ShowResult(resultTextBlock, "⚠️ Please enter an API key first", Colors.Orange);
                return;
            }

            button.IsEnabled = false;
            ShowResult(resultTextBlock, "🔄 Testing API key...", Colors.White);

            try
            {
                using var client = new HttpClient();
                
                var requestBody = new
                {
                    contents = new[] { new { parts = new[] { new { text = "Say OK" } } } },
                    generationConfig = new { maxOutputTokens = 5 }
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Use gemini-2.0-flash-001 for testing (stable model)
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-001:generateContent?key={plugin.GoogleApiKey}";
                var response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    ShowResult(resultTextBlock, "✅ Google API key is valid!", Colors.LightGreen);
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var errorMsg = $"❌ API Error ({response.StatusCode})";
                    
                    try
                    {
                        var errorJson = JObject.Parse(responseContent);
                        var message = errorJson["error"]?["message"]?.ToString();
                        if (!string.IsNullOrEmpty(message))
                        {
                            errorMsg = $"❌ {message}";
                        }
                    }
                    catch { }
                    
                    ShowResult(resultTextBlock, errorMsg, Colors.Salmon);
                }
            }
            catch (Exception ex)
            {
                HandleApiError(resultTextBlock, ex);
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        #endregion

        #region Ollama Handlers

        private void GetOllama_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://ollama.ai/download",
                UseShellExecute = true
            });
        }

        private async void TestOllama_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is not AIAssistantPlugin plugin) return;

            var resultTextBlock = FindTextBlock("OllamaTestResult", button);
            if (resultTextBlock == null) return;

            button.IsEnabled = false;
            ShowResult(resultTextBlock, "🔄 Testing Ollama connection...", Colors.White);

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                
                var endpoint = plugin.OllamaEndpoint ?? "http://localhost:11434";
                var response = await client.GetAsync($"{endpoint}/api/tags");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    var models = json["models"]?.ToObject<JArray>();
                    var modelCount = models?.Count ?? 0;
                    
                    ShowResult(resultTextBlock, $"✅ Ollama is running! {modelCount} model(s) available.", Colors.LightGreen);
                }
                else
                {
                    ShowResult(resultTextBlock, $"❌ Ollama responded with: {response.StatusCode}", Colors.Salmon);
                }
            }
            catch (HttpRequestException)
            {
                ShowResult(resultTextBlock, "❌ Cannot connect. Make sure Ollama is running.", Colors.Salmon);
            }
            catch (Exception ex)
            {
                HandleApiError(resultTextBlock, ex);
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        #endregion

        #region MCP Handlers

        private async void TestMCPConnection_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is not AIAssistantPlugin plugin) return;

            var resultTextBlock = FindTextBlock("MCPTestResult", button);
            if (resultTextBlock == null) return;

            button.IsEnabled = false;
            ShowResult(resultTextBlock, "🔄 Testing NINA Advanced API connection...", Colors.White);

            try
            {
                var mcpConfig = plugin.GetMCPConfig();
                var client = new AI.MCP.NINAAdvancedAPIClient();
                var connected = await client.InitializeAsync(mcpConfig);

                if (connected)
                {
                    ShowResult(resultTextBlock, $"✅ Connected to NINA Advanced API at {mcpConfig.NinaHost}:{mcpConfig.NinaPort}", Colors.LightGreen);
                }
                else
                {
                    ShowResult(resultTextBlock, "❌ Could not connect. Ensure NINA is running with Advanced API plugin enabled.", Colors.Salmon);
                }
                
                client.Close();
            }
            catch (Exception ex)
            {
                ShowResult(resultTextBlock, $"❌ Connection failed: {ex.Message}", Colors.Salmon);
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private void GetAdvancedAPIPlugin_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/christian-photo/ninaAPI",
                UseShellExecute = true
            });
        }

        private void GetMCPRepo_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/michelebergo/nina_mcp_server",
                UseShellExecute = true
            });
        }

        private void GetExternalMCPDocs_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/michelebergo/nina-ai-assistant/blob/main/EXTERNAL_MCP_SETUP.md",
                UseShellExecute = true
            });
        }

        private void GetExternalMCPExample_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/michelebergo/nina-ai-assistant/blob/main/nina_advanced_api_mcp_server.py",
                UseShellExecute = true
            });
        }

        #endregion

        #region Model Loading

        private async Task LoadModelsForProvider(AIProviderType providerType, AIAssistantPlugin plugin)
        {
            try
            {
                IAIProvider? provider = null;
                ComboBox? modelComboBox = null;
                string? currentModel = null;

                // Find the ComboBox and create provider based on type
                switch (providerType)
                {
                    case AIProviderType.GitHub:
                        modelComboBox = FindControl<ComboBox>("GitHubModelComboBox");
                        currentModel = plugin.GitHubModelId;
                        if (!string.IsNullOrWhiteSpace(plugin.GitHubApiKey))
                        {
                            provider = new GitHubModelsProvider();
                            await provider.InitializeAsync(new AIProviderConfig
                            {
                                ApiKey = plugin.GitHubApiKey,
                                ModelId = currentModel
                            });
                        }
                        break;

                    case AIProviderType.OpenAI:
                        modelComboBox = FindControl<ComboBox>("OpenAIModelComboBox");
                        currentModel = plugin.OpenAIModelId;
                        if (!string.IsNullOrWhiteSpace(plugin.OpenAIApiKey))
                        {
                            provider = new OpenAIProvider();
                            await provider.InitializeAsync(new AIProviderConfig
                            {
                                ApiKey = plugin.OpenAIApiKey,
                                ModelId = currentModel
                            });
                        }
                        break;

                    case AIProviderType.Anthropic:
                        modelComboBox = FindControl<ComboBox>("AnthropicModelComboBox");
                        currentModel = plugin.AnthropicModelId;
                        if (!string.IsNullOrWhiteSpace(plugin.AnthropicApiKey))
                        {
                            provider = new AnthropicProvider();
                            await provider.InitializeAsync(new AIProviderConfig
                            {
                                ApiKey = plugin.AnthropicApiKey,
                                ModelId = currentModel
                            });
                        }
                        break;

                    case AIProviderType.Google:
                        modelComboBox = FindControl<ComboBox>("GoogleModelComboBox");
                        currentModel = plugin.GoogleModelId;
                        if (!string.IsNullOrWhiteSpace(plugin.GoogleApiKey))
                        {
                            provider = new GoogleProvider();
                            await provider.InitializeAsync(new AIProviderConfig
                            {
                                ApiKey = plugin.GoogleApiKey,
                                ModelId = currentModel
                            });
                        }
                        break;

                    case AIProviderType.Ollama:
                        modelComboBox = FindControl<ComboBox>("OllamaModelComboBox");
                        currentModel = plugin.OllamaModelId;
                        provider = new OllamaProvider();
                        await provider.InitializeAsync(new AIProviderConfig
                        {
                            ApiKey = string.Empty,
                            ModelId = currentModel,
                            Endpoint = plugin.OllamaEndpoint
                        });
                        break;
                }

                if (modelComboBox != null && provider != null)
                {
                    var models = await provider.GetAvailableModelsAsync();
                    
                    modelComboBox.Items.Clear();
                    foreach (var model in models)
                    {
                        var item = new ComboBoxItem { Content = model };
                        if (model == currentModel)
                            item.IsSelected = true;
                        modelComboBox.Items.Add(item);
                    }

                    // If current model not in list, add it and select it
                    if (!string.IsNullOrEmpty(currentModel) && !models.Any(m => m == currentModel))
                    {
                        var item = new ComboBoxItem { Content = currentModel, IsSelected = true };
                        modelComboBox.Items.Insert(0, item);
                    }

                    NINA.Core.Utility.Logger.Info($"Loaded {models.Length} models for {providerType}");
                }
            }
            catch (Exception ex)
            {
                NINA.Core.Utility.Logger.Error($"Failed to load models for {providerType}: {ex.Message}");
            }
        }

        private T? FindControl<T>(string name) where T : FrameworkElement
        {
            return FindControlInVisualTree<T>(Application.Current.MainWindow, name);
        }

        private T? FindControlInVisualTree<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;

            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                
                if (child is T element && element.Name == name)
                    return element;

                var result = FindControlInVisualTree<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        #endregion

        #region Helper Methods

        private TextBlock? FindTextBlock(string name, FrameworkElement startElement)
        {
            var parent = startElement.Parent as FrameworkElement;
            while (parent != null)
            {
                if (parent is StackPanel stackPanel)
                {
                    foreach (var child in stackPanel.Children)
                    {
                        if (child is TextBlock tb && tb.Name == name)
                        {
                            return tb;
                        }
                    }
                }
                parent = parent.Parent as FrameworkElement;
            }
            return null;
        }

        private void ClearTestResult(string textBlockName, object sender)
        {
            if (sender is FrameworkElement element)
            {
                var tb = FindTextBlock(textBlockName, element);
                if (tb != null)
                {
                    tb.Text = "";
                }
            }
        }

        private void ShowResult(TextBlock textBlock, string message, Color color)
        {
            textBlock.Foreground = new SolidColorBrush(color);
            textBlock.Text = message;
        }

        private void HandleApiError(TextBlock resultTextBlock, Exception ex)
        {
            if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
            {
                ShowResult(resultTextBlock, "❌ Invalid API key. Please check your key.", Colors.Salmon);
            }
            else if (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                ShowResult(resultTextBlock, "❌ Connection timeout. Server may be unavailable.", Colors.Salmon);
            }
            else
            {
                ShowResult(resultTextBlock, $"❌ Error: {ex.Message}", Colors.Salmon);
            }
        }

        #endregion
    }
}
