namespace NINA.Plugin.AIAssistant.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.0.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool UpdateSettings {
            get {
                return ((bool)(this["UpdateSettings"]));
            }
            set {
                this["UpdateSettings"] = value;
            }
        }

        // Active Provider Selection
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        // Ollama, not GitHub. GitHub Models was retired on 2026-07-30, so a fresh install used to
        // open on a provider that cannot work and fail the user's first message with an error
        // telling them to change provider. Ollama is the one choice that needs no key: if it is
        // not installed the failure names a local address, which points at a fix.
        [global::System.Configuration.DefaultSettingValueAttribute("Ollama")]
        public string SelectedProvider {
            get {
                return ((string)(this["SelectedProvider"]));
            }
            set {
                this["SelectedProvider"] = value;
            }
        }

        // GitHub Models Settings
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string GitHubApiKey {
            get {
                return ((string)(this["GitHubApiKey"]));
            }
            set {
                this["GitHubApiKey"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("gpt-4o")]
        public string GitHubModelId {
            get {
                return ((string)(this["GitHubModelId"]));
            }
            set {
                this["GitHubModelId"] = value;
            }
        }

        // OpenAI Settings
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string OpenAIApiKey {
            get {
                return ((string)(this["OpenAIApiKey"]));
            }
            set {
                this["OpenAIApiKey"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("gpt-4o")]
        public string OpenAIModelId {
            get {
                return ((string)(this["OpenAIModelId"]));
            }
            set {
                this["OpenAIModelId"] = value;
            }
        }

        // Anthropic Settings
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string AnthropicApiKey {
            get {
                return ((string)(this["AnthropicApiKey"]));
            }
            set {
                this["AnthropicApiKey"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("claude-sonnet-4-5-20250929")]
        public string AnthropicModelId {
            get {
                return ((string)(this["AnthropicModelId"]));
            }
            set {
                this["AnthropicModelId"] = value;
            }
        }

        // Google Gemini Settings
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string GoogleApiKey {
            get {
                return ((string)(this["GoogleApiKey"]));
            }
            set {
                this["GoogleApiKey"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("gemini-2.0-flash-exp")]
        public string GoogleModelId {
            get {
                return ((string)(this["GoogleModelId"]));
            }
            set {
                this["GoogleModelId"] = value;
            }
        }

        // Ollama Settings (Local)
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("http://localhost:11434")]
        public string OllamaEndpoint {
            get {
                return ((string)(this["OllamaEndpoint"]));
            }
            set {
                this["OllamaEndpoint"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("llama3.2")]
        public string OllamaModelId {
            get {
                return ((string)(this["OllamaModelId"]));
            }
            set {
                this["OllamaModelId"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool OllamaDisableThinking {
            get {
                return ((bool)(this["OllamaDisableThinking"]));
            }
            set {
                this["OllamaDisableThinking"] = value;
            }
        }

        // MCP (Model Context Protocol) Settings for NINA Advanced API
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool MCPEnabled {
            get {
                return ((bool)(this["MCPEnabled"]));
            }
            set {
                this["MCPEnabled"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("localhost")]
        public string MCPNinaHost {
            get {
                return ((string)(this["MCPNinaHost"]));
            }
            set {
                this["MCPNinaHost"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("1888")]
        public int MCPNinaPort {
            get {
                return ((int)(this["MCPNinaPort"]));
            }
            set {
                this["MCPNinaPort"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("python")]
        public string ExternalMCPPythonPath {
            get {
                return ((string)(this["ExternalMCPPythonPath"]));
            }
            set {
                this["ExternalMCPPythonPath"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string ExternalMCPScriptPath {
            get {
                return ((string)(this["ExternalMCPScriptPath"]));
            }
            set {
                this["ExternalMCPScriptPath"] = value;
            }
        }

        // ---- Phase 5: nina.autopilot orchestrator integration ----

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool OrchestratorEnabled {
            get {
                return ((bool)(this["OrchestratorEnabled"]));
            }
            set {
                this["OrchestratorEnabled"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("http://127.0.0.1:8765")]
        public string OrchestratorUrl {
            get {
                return ((string)(this["OrchestratorUrl"]));
            }
            set {
                this["OrchestratorUrl"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("5")]
        public int OrchestratorPollIntervalSeconds {
            get {
                return ((int)(this["OrchestratorPollIntervalSeconds"]));
            }
            set {
                this["OrchestratorPollIntervalSeconds"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("{\n  \"mcpServers\": {\n    \"nina-advanced-api\": {\n      \"command\": \"python\",\n      \"args\": [\"nina_advanced_api_mcp_server.py\"],\n      \"env\": {\n        \"NINA_HOST\": \"localhost\",\n        \"NINA_PORT\": \"1888\"\n      },\n      \"enabled\": false\n    }\n  }\n}")]
        public string ExternalMCPServersJson {
            get {
                return ((string)(this["ExternalMCPServersJson"]));
            }
            set {
                this["ExternalMCPServersJson"] = value;
            }
        }

        // Mistral AI Settings
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string MistralApiKey {
            get {
                return ((string)(this["MistralApiKey"]));
            }
            set {
                this["MistralApiKey"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("mistral-large-latest")]
        public string MistralModelId {
            get {
                return ((string)(this["MistralModelId"]));
            }
            set {
                this["MistralModelId"] = value;
            }
        }
    }
}
