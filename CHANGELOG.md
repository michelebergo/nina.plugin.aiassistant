# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0.0] - 2025-07-10

### Added
- 🌐 **Google Gemini MCP Support** - Google Gemini now supports MCP (Model Context Protocol) for direct equipment control, joining Anthropic Claude as a second MCP-capable provider
- 🧠 **Anti-hallucination System Prompts** - All 5 providers now have comprehensive system prompts with strict rules to prevent fabrication of equipment specs, NINA features, or astrophotography data
- 🔄 **Dynamic Model Discovery** - All providers now fetch available models from their APIs with 1-hour cache, instead of relying on hardcoded model lists
- ⚙️ **OpenAI Reasoning Model Support** - Smart handling of `max_completion_tokens` and temperature parameters for o1/o3/o4-mini and newer GPT models, with auto-retry fallback

### Fixed
- 🐛 **GitHub Models System Prompt Bug** - Fixed bug where GitHub Models provider ignored the system prompt and used a hardcoded message instead
- 🐛 **Google MCP Prompt Routing Bug** - Fixed critical bug in AIChatVM where Google+MCP always received the generic prompt instead of the MCP-aware prompt, preventing tool calls from working
- 🐛 **MCP Settings UI for Google** - MCP configuration section in Options was only visible for Anthropic; now shows for both Anthropic and Google
- 🐛 **Anthropic Model ID Errors** - Fixed incorrect model IDs (e.g. `claude-sonnet-4.5`) with proper versioned IDs from API, added model-not-found fallback
- 🐛 **OpenAI Temperature Rejection** - Reasoning models (o1/o3/o4) reject non-default temperature; now auto-detected and skipped

### Removed
- ❌ **OpenRouter Provider** - Completely removed from codebase (enum, UI, settings, provider class)

### Changed
- All provider fallback prompts upgraded from vague descriptions to detailed, scope-limited astrophotography-focused prompts
- MCP settings description updated to mention both Anthropic Claude and Google Gemini
- Non-chat models now filtered from OpenAI and GitHub Models lists
- Plugin now reports "5 AI PROVIDERS" (was 6)

---

## [2.0.2.0] - 2025-07-04

### Fixed
- Bug fixes and stability improvements

---

## [2.0.0.0] - 2025-06-15

### Added
- Major update with MCP support for Anthropic Claude
- External MCP server support
- Improved model selection

---

## [1.0.0] - 2025-01-18

### Added
- 🎉 **Initial release**

#### Multi-Provider AI Support
- GitHub Models (free)
- OpenAI (GPT-4o, GPT-4o-mini)
- Anthropic Claude (with MCP support)
- Google Gemini (free tier available)
- Ollama (local, free)

#### MCP Equipment Control (Anthropic Claude)
- Camera control: connect, capture, cooling, abort
- Mount control: slew, park/unpark, tracking modes
- Focuser control: move to position
- Filter wheel control: change filters
- Guider control: start/stop guiding, calibration
- Dome control: open/close shutter, park

#### User Interface
- Interactive chat panel (dockable)
- Provider selection with status indicator
- Secure API key storage
- MCP connection testing
- Real-time equipment status queries

#### Settings & Configuration
- Persistent settings across sessions
- Per-provider model selection
- MCP host/port configuration
- Connection testing for all providers
