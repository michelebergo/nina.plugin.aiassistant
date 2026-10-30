# NINA AI Assistant

🤖 AI-powered astrophotography assistant for [N.I.N.A.](https://nighttime-imaging.eu/) with MCP equipment control.

![NINA 3.x](https://img.shields.io/badge/NINA-3.x-blue?style=flat-square)
![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple?style=flat-square)
![License](https://img.shields.io/badge/License-MPL--2.0-green?style=flat-square)
[![Buy Me a Coffee](https://img.shields.io/badge/Buy%20Me%20a%20Coffee-Support-yellow?logo=buymeacoffee&logoColor=black)](https://buymeacoffee.com/michelebergo)

## Features

- **Multi-Provider AI**: Ollama (local, free), Google Gemini, Mistral AI, OpenAI, Anthropic Claude — (GitHub Models was retired by GitHub on July 30, 2026)
- **MCP Equipment Control**: Natural language control of cameras, mounts, focusers, filter wheels, guiders, dome - with every provider (Ollama with a tool-capable model)
- **Dynamic Model Discovery**: Automatic detection of latest AI models from each provider
- **Image Analysis**: FITS header reading, HFR/FWHM monitoring, star detection, quality assessment
- **Dockable Chat Panel**: Integrated AI chat within NINA's imaging tab
- **Knowledge Wiki (second brain)**: A local markdown knowledge base the assistant consults before answering — your equipment quirks, solved problems and site notes take precedence over the model's general knowledge
- **Activity trace**: See what the assistant is doing while it answers — one line for the tool in progress, expandable afterwards into the full sequence with each call's arguments and outcome
- **Context and cost readout**: How full the model's context window is, how many tokens are left, and what the conversation has cost so far
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

The options page is laid out in the order you set things up:

1. **AI provider** – pick one (see the table below first), paste the key (not needed for Ollama; Google Gemini and Mistral offer free API tiers), choose the model, press Test. For Ollama, the model's "thinking" phase is disabled by default ("Disable model thinking") so thinking-capable models (Gemma 4, Qwen 3.x, DeepSeek) answer promptly instead of reasoning at length first.
2. **Equipment control** – enable MCP and point it at the NINA Advanced API plugin (host and port, Test connection). Works with every provider.
3. **Your rig** – whether a short summary of your active NINA profile (site, telescope, camera, image paths) travels with each message so the assistant stops guessing your own equipment. On by default; the exact text goes to the NINA log every time it changes.
4. **External MCP servers** and **Autopilot connection** – advanced, optional.

Then start chatting in the dockable AI panel.

### What each provider can do

Check this before buying credits.

| Provider | Cost | Chat | Equipment control (MCP) | External MCP servers | Notes |
|---|---|---|---|---|---|
| Ollama | free, runs on your machine | ✅ | ✅ with a tool-capable model (llama3.1+, qwen2.5, …) | ✅ | nothing leaves your PC |
| Google Gemini | free tier | ✅ | ✅ | ✅ | |
| Anthropic Claude | paid | ✅ | ✅ | ✅ | |
| OpenAI | paid | ✅ | ✅ (since 2.5.7.0) | ✅ | newest reasoning models (gpt-6-astra…) are moved to the Responses API automatically |
| Mistral AI | free tier / paid | ✅ | ✅ (since 2.5.7.0) | ✅ | |
| GitHub Models | — | ❌ retired by GitHub on 30 July 2026 | ❌ | ❌ | listed only if you still have it selected |

"Equipment control" means the assistant receives the NINA tool definitions, calls them (status, connect, capture, slew, …) and sees the results, in as many rounds as the task needs. OpenAI and Mistral speak the same `/chat/completions` tool-calling dialect, so they share one loop; the newest OpenAI models, which call tools only through the Responses API, are moved there automatically the first time they say so. Anthropic, Gemini and Ollama each have their own loop. OpenAI accepts at most 128 tools per request: the least conversational ones (rescans, device listings, one-by-one disconnects, …) are left out first, and the NINA log lists what was dropped.

If you want an agent with its own permissions, workspace and session memory that works your equipment with any provider, see [OpenCode for N.I.N.A.](https://github.com/michelebergo/nina.plugin.opencode): it drives the opencode agent with NINA's tools via `nina_mcp_server`, and is the plugin meant for the more advanced, agentic use.

## Knowledge Wiki

With MCP enabled, the assistant has a personal knowledge wiki at
`%LOCALAPPDATA%\NINA\llmwiki` — plain markdown pages you can read and edit with
any editor. On first use it is seeded with a starter pack (troubleshooting
checklists, page templates and a schema).

- **The assistant reads it first**: questions about your specific equipment,
  site, or a problem you hit before are answered from the wiki (`wiki_search` /
  `wiki_read` tools), citing the page used.
- **It can remember things**: when the conversation surfaces a durable fact
  ("my camera shows amp glow above gain 350"), the assistant offers to save it.
  A confirmation dialog always shows the exact text before anything is written;
  notes land append-only in `raw/`, never in consolidated pages.
- **Shared second brain**: the AI Weather plugin appends daily sky digests to
  the same wiki, and the [nina.autopilot](https://github.com/michelebergo/nina.autopilot)
  agent (`--wiki-ingest`) consolidates all raw notes into proper wiki pages.
- Add your own pages any time: copy the templates in `wiki/entities/` and the
  assistant will find them.

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
│       ├── MistralProvider.cs     # Mistral AI
│       ├── OllamaProvider.cs      # Ollama with MCP tools
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

Issues and pull requests ask whether an AI tool helped write them or analyse the data behind them. Please answer honestly; it is not a judgement, it tells me how to read what you send.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## Support the project

This plugin is developed and maintained independently in my free time.

If it is useful for your astrophotography or automation workflow, you can support its continued development:

[☕ Support development on Buy Me a Coffee](https://buymeacoffee.com/michelebergo)

Your support helps cover development tools, testing hardware, hosting and future features.

## License and names

AI Assistant is released under the Mozilla Public License 2.0 — see [LICENSE.txt](LICENSE.txt).

**Author: Michele Bergo.** Every copy and every derivative work must keep the copyright
notice and credit Michele Bergo as the original author, as the license requires. The MPL
grants no rights to names (section 2.3): "AI Weather", "AI Assistant" and "OpenCode" (for
N.I.N.A.) are the names of Michele Bergo's plugins, and a fork or derivative work must be
published under a different name — not one of these nor a confusable variant such as
"AI Weather Advanced" — with its own plugin identifier (GUID), and must not present Michele
Bergo as the author of the modified version. See [NOTICE](NOTICE).

## Acknowledgments

- [N.I.N.A.](https://nighttime-imaging.eu/) - The amazing astrophotography software
- [NINA Advanced API](https://github.com/christian-photo/ninaAPI) - API integration for NINA
- The astrophotography community
