# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.3.0.0] - 2026-03-03

### Added
- 🔭 **Profile Management Tools** - New `nina_show_profile`, `nina_change_profile_value`, `nina_switch_profile`, and `nina_get_horizon` tools for viewing and modifying NINA profiles via conversation
- 📋 **Extended Sequence Control** - New `nina_sequence_state`, `nina_sequence_edit`, `nina_sequence_skip`, `nina_sequence_reset`, `nina_sequence_list_available`, and `nina_sequence_set_target` tools for full sequence management
- 📸 **Image Retrieval Tools** - New `nina_get_image` and `nina_get_thumbnail` tools to retrieve captured images by index
- 📜 **Event History** - New `nina_get_event_history` tool to review equipment events, captures, and errors
- 🌤️ **Sky Flat Workflows** - 5 new specialized flat frame tools: `nina_skyflat`, `nina_auto_brightness_flat`, `nina_auto_exposure_flat`, `nina_trained_dark_flat`, `nina_trained_flat` with full parameter support
- 🔧 **Missing Equipment Tools** - Added `nina_list_camera_devices`, `nina_list_dome_devices`, `nina_home_dome`, `nina_list_filterwheel_devices`, `nina_list_guider_devices`, `nina_get_guider_graph`, `nina_get_flats_status`

### Fixed
- 🐛 **Dome Follow Parameter** - Fixed `nina_set_dome_follow` sending wrong parameter name (`enable` → `enabled`)
- 🐛 **Flat Panel Light Parameter** - Fixed `nina_set_flatpanel_light` sending wrong parameter name (`power` → `on`)
- 🐛 **Rotator Reverse Parameter** - Fixed `nina_set_rotator_reverse` sending wrong parameter name (`enabled` → `reverseDirection`)
- 🐛 **Filter Operations** - Fixed `nina_remove_filter` and `nina_get_filter_info` using wrong parameter (`position` → `filterId`)
- 🐛 **Dome Sync** - Fixed `nina_sync_dome` sending unsupported `azimuth` parameter (API accepts no parameters)
- 🐛 **Dome Slew** - Added missing `waitToFinish` parameter to `nina_slew_dome`
- 🐛 **Focuser Move** - Removed non-existent `relative` parameter from `nina_move_focuser`
- 🐛 **Rotator Move** - Removed non-existent `relative` parameter from `nina_move_rotator`
- 🐛 **Rotator Range** - Fixed `nina_set_rotator_mechanical_range` to use correct `range` enum (full/half/quarter) + `rangeStartPosition` instead of incorrect `min`/`max`
- 🐛 **Autofocus** - Fixed `nina_start_autofocus` removing non-existent `method` parameter
- 🐛 **Image History** - Fixed `nina_get_image_history` to use correct API parameters (`all`/`index`/`count`/`imageType`) instead of wrong `limit`/`offset`
- 🐛 **Add Filter** - Fixed `nina_add_filter` to send no parameters (API accepts none)
- 🐛 **Flats Endpoint** - Replaced broken `nina_start_flats` (mapped to non-existent `flats/start`) with 5 correct flat-type endpoints

### Removed
- ❌ `nina_calibrate_guider` - Removed (endpoint doesn't exist; use `nina_start_guiding` with `calibrate=true`)
- ❌ `nina_sync_rotator` - Removed (endpoint `rotator/sync` doesn't exist in API)
- ❌ `nina_set_camera_gain` - Removed (endpoint `camera/set-gain` doesn't exist; gain is set via capture)
- ❌ `nina_set_camera_offset` - Removed (endpoint `camera/set-offset` doesn't exist; offset is set via capture)
- ❌ `nina_get_autofocus_status` - Removed (mapped to `auto-focus` which would inadvertently start autofocus)
- ❌ `nina_start_flats` - Replaced with 5 specific flat-type tools matching the API

---

## [2.2.0.0] - 2026-03-01

### Added
- 📊 **Universal Quota Monitoring** - Added real-time tracking of API limits and token usage for all providers (Anthropic, OpenAI, Google, GitHub, and Ollama)
- 📝 **Detailed Token Usage** - New breakdown showing input vs output tokens in the chat header (e.g., `120 in | 45 out`)
- ⏱️ **Proactive Rate Limit Info** - When rate limited, the error message now includes the exact time until your quota resets
- ✨ **Enhanced Header UI** - New `Goldenrod` styled quota indicator and improved model information layout

### Fixed
- 🐛 **Google Gemini MCP Collision** - Fixed "Duplicate function declaration" errors by detecting and skipping tool name collisions with NINA's built-in tools
- 🐛 **MCP Status Caching** - Fixed bug where switching between Gemini and Anthropic would fail to initialize MCP due to stale initialization flags
- 🐛 **Anthropic Connection Testing** - Fixed logic to properly use the selected model during API connection tests
- 🐛 **Claude 3.5 Sonnet Naming** - Updated default Anthropic model to the latest `claude-3-5-sonnet-20241022`
- 🛠️ **XAML Stability** - Resolved build issues related to dictionary extensions and missing visibility converters

---

## [2.1.2.0] - 2026-02-15

### Fixed
- 🐛 **Manifest Format** - Restructured manifest.json to match Stefan Berg's `CreateManifest.ps1` format: `ShortDescription`/`LongDescription` moved inside `Descriptions` object, replaced invalid `IconURL` with `FeaturedImageURL`, added `Homepage`, `ScreenshotURL`, `AltScreenshotURL` fields
- 🐛 **CI/CD Manifest Path** - Fixed `PLUGIN_MANIFEST_PATH` in GitHub Actions workflow from `a/AIAssistant` to `a/aiassistant/3.0.0` to match the actual directory structure in `nina.plugin.manifests` repository

---

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
