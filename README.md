# NINA AI Assistant

🤖 AI-powered astrophotography assistant for [N.I.N.A.](https://nighttime-imaging.eu/) with MCP equipment control.

![NINA 3.x](https://img.shields.io/badge/NINA-3.x-blue?style=flat-square)
![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple?style=flat-square)
![License](https://img.shields.io/badge/License-MPL--2.0-green?style=flat-square)

## Features

- **Multi-Provider AI**: GitHub Models (free), OpenAI, Anthropic Claude, Google Gemini, Ollama (local)
- **MCP Equipment Control**: Natural language control of cameras, mounts, focusers, filter wheels, guiders, dome
- **Dynamic Model Discovery**: Automatic detection of latest AI models from each provider
- **Image Analysis**: FITS header reading, HFR/FWHM monitoring, star detection, quality assessment
- **Dockable Chat Panel**: Integrated AI chat within NINA's imaging tab
- **Extensible**: Connect external MCP servers for astronomy calculations, weather, catalogs

## Installation

### From NINA Plugin Manager (Recommended)
1. Open NINA → Options → Plugins
2. Search for "AI Assistant"
3. Click Install
4. Restart NINA

### Manual Installation
1. Download the latest release from [Releases](https://github.com/michelebergo/nina.plugin.aiassistant/releases)
2. Extract to `%localappdata%\NINA\Plugins\3.0.0\AI Assistant\`
3. Restart NINA

## Configuration

1. Go to **Options → Plugins → AI Assistant**
2. Select your AI provider
3. Enter your API key (not required for GitHub Models free tier or Ollama)
4. Start chatting in the dockable AI panel

## Building from Source

### Prerequisites
- Visual Studio 2022 or later
- .NET 8.0 SDK
- NINA 3.x installed (for local testing only)

### Build
```bash
git clone https://github.com/michelebergo/nina.plugin.aiassistant.git
cd nina.plugin.aiassistant
dotnet restore
dotnet build -c Release
```

The post-build event copies the plugin to your local NINA plugins folder.

## Project Structure
```
nina.plugin.aiassistant/
├── AI/
│   ├── AIService.cs              # Main AI service orchestrator
│   ├── IAIProvider.cs            # Provider interface
│   ├── Models.cs                 # Request/Response models
│   ├── MCP/
│   │   ├── MCPModels.cs          # MCP data structures
│   │   └── NINAAdvancedAPIClient.cs  # NINA API integration
│   └── Providers/
│       ├── AnthropicProvider.cs  # Claude with MCP tools
│       ├── GitHubModelsProvider.cs
│       ├── GoogleProvider.cs
│       ├── OllamaProvider.cs
│       └── OpenAIProvider.cs
├── MCP/
│   └── ExternalMCPClient.cs      # External MCP server support
├── Properties/
│   ├── AssemblyInfo.cs           # Plugin metadata
│   └── Settings.Designer.cs     # User settings
├── Resources/
│   └── Icons.xaml                # UI icons
├── AIAssistantPlugin.cs          # Plugin entry point
├── AIChatVM.cs                   # Chat panel view model
├── AIChatTemplate.xaml           # Chat UI template
├── AIChatView.xaml               # Chat view (dockable panel)
├── Options.xaml                  # Settings UI
├── NINA.Plugin.AIAssistant.csproj
├── NINA.Plugin.AIAssistant.sln
├── manifest.json                 # Plugin manifest
├── icon.png
├── LICENSE.txt
├── CHANGELOG.md
└── README.md
```

## Releasing

This repository uses the [official NINA plugin GitHub Action](https://github.com/isbeorn/nina.plugin.manifests/blob/main/tools/github-action.yaml). To release a new version:

1. Update version in `Properties/AssemblyInfo.cs`
2. Update `CHANGELOG.md`
3. Commit and push
4. Tag the commit: `git tag 2.1.0.0 && git push origin 2.1.0.0`
5. The GitHub Action will build, create a release, and submit a manifest PR

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the Mozilla Public License 2.0 - see the [LICENSE.txt](LICENSE.txt) file for details.

## Acknowledgments

- [N.I.N.A.](https://nighttime-imaging.eu/) - The amazing astrophotography software
- [NINA Advanced API](https://github.com/christian-photo/ninaAPI) - API integration for NINA
- The astrophotography community
