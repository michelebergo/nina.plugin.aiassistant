# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.5.3.0] - 2026-08-15

### Added
- **Knowledge Wiki (second brain)** - a local markdown knowledge base at `%LOCALAPPDATA%\NINA\llmwiki`, seeded on first use with troubleshooting checklists and page templates. With MCP enabled the assistant searches it before answering (`wiki_index`/`wiki_search`/`wiki_read`): facts about YOUR equipment, site and solved problems take precedence over the model's general knowledge, and the page used is cited.
- **"Remember this" with a real consent gate** (`wiki_append`) - when the chat surfaces a durable fact, the assistant can save it to the wiki's append-only `raw/` notes; a confirmation dialog always shows the exact text before anything touches disk (small local models proved they skip prompt-level consent). Consolidation into proper pages is done by the separate [nina.autopilot](https://github.com/michelebergo/nina.autopilot) ingest agent, which also processes the AI Weather daily digests written to the same shared wiki.
- **"Disable model thinking" toggle in the Ollama options** (on by default) - the 2.5.2.0 behavior is now user-controllable for those who want thinking on fast hardware.

### Changed
- Options panel: checkboxes and button rows are now consistently left-aligned.

## [2.5.2.0] - 2026-08-15

### Fixed
- **Ollama thinking-capable models (Gemma 4, Qwen 3.x, DeepSeek) were slow or returned empty replies** (fixes #8) - Ollama enables a "thinking" phase by default on newer models: the model reasons at length before answering (77.6s vs 14.2s field-measured on the same request), and on some runs the actual answer lands in a separate `thinking` field while `content` comes back empty. Requests now send `think: false` by default, and the response parser recovers answers from the `thinking` field and strips inline `<think>` blocks. Thanks to Alexandre (@Dhraks) for the report and the before/after measurements.

## [2.5.1.0] - 2026-08-10

### Fixed
- **Google Gemini "Test" button** - it called a hardcoded `gemini-2.0-flash-001`, which Google has retired, so the test failed even when the selected model worked fine. The test now uses the model you actually selected (#7 reporters migrating to Gemini hit this first).
- **Stale Gemini defaults** - all fallbacks now use the `gemini-flash-latest` alias, which always points to Google's latest stable Flash release and therefore cannot expire; the static model list (used only when the live fetch fails) was refreshed to the current generation (3.6/3.5/2.5).

### Changed
- **GitHub Models provider marked as retired** (fixes #7) - GitHub shut the service down for all customers on July 30, 2026, which is why it returns 404 everywhere. The provider no longer calls the dead endpoint: the chat, the options panel and the Test button now show a clear explanation and point to the free alternatives already included (Ollama local, Google Gemini and Mistral free tiers). Existing configurations keep loading without errors.

---

## [2.5.0.3] - 2026-08-09

### Fixed
- **MCP fetch server startup** - the built-in `fetch` MCP server config now pins the Python MCP SDK to `<2.0.0` (`uvx --with "mcp<2.0.0" mcp-server-fetch ...`). This works around a breaking rename (`McpError` → `MCPError`) in MCP SDK 2.0.0 that prevented the server from starting on fresh installs. Drop the pin once modelcontextprotocol/servers#4560 is resolved.

---

## [2.5.0.2] - 2026-08-02

### Fixed
- **Mistral provider selection** - selecting Mistral AI now shows its configuration section (API key, model selection); the UI property notification was missing. Thanks to @stephantamminga for the fix (#6, fixes #5).
- **Mistral model dropdown** - models are now loaded live from the Mistral API once a key is configured, consistent with the other providers, instead of a static hardcoded list.

---

## [2.5.0.1] - 2026-08-02

### Added
- **Real-time tool progress** - the chat activity indicator now reports the current tool-calling iteration and tool name for Anthropic, Google Gemini and Ollama.
- **Bounded MCP execution** - Anthropic tool calls time out after 90 seconds and external MCP response reads after 120 seconds, preventing a stalled server from freezing the plugin indefinitely.

### Fixed
- **Google Gemini diagnostics** - HTTP failures, empty candidates, finish reasons, prompt feedback and safety blocks now produce actionable errors instead of a generic empty-response message.
- **Anthropic tool routing** - built-in to external MCP fallback now handles timeouts and tool failures as structured results instead of leaking exceptions.
- **NINA catalog metadata** - provider count, Mistral/Ollama/Orchestrator tags and the plugin identifier now match the generated public manifest.

---

## [2.5.0.0] - 2026-07-23

### Added
- 🤖 **Mistral AI provider** - Mistral models (default `mistral-large-latest`) join GitHub Models, OpenAI, Anthropic, Google and Ollama.
- 🛰️ **Orchestrator integration panel** - optional integration with the nina.autopilot orchestrator dashboard (disabled by default; URL and poll interval configurable).
- ☕ **Support the project** - optional Buy Me a Coffee link in the Options page, README and GitHub Sponsor button. Completely non-intrusive: no popups, no nags, no telemetry.

---

## [2.4.1.2] - 2026-06-27

### Added
- 💬 **Conversation History** - The assistant now sends the full prior conversation with each message, so follow-up questions and pronoun references ("how long should I expose *it*?") work. Works across all providers. Use **Clear** to reset the context.
- 🤖 **External MCP Tools for Claude** - Anthropic Claude can now use external MCP servers (web fetch/search, memory, Context7, etc.), matching Google Gemini and Ollama.

### Fixed
- 🐛 **Gemini Tool Schema (400)** - Google Gemini rejected tools whose array parameters lacked an `items` declaration (e.g. memory/filesystem servers). The full JSON schema is now preserved and sanitized so arrays declare `items` and objects keep `properties`.
- 🐛 **Model Change Propagation** - Selecting a different model now re-initializes the active provider immediately; previously the change only applied after switching providers back and forth.

### Changed
- 🔍 **Ollama Model Auto-Detection** - The model list reliably reflects models installed on the server: it re-queries `/api/tags` when you open the dropdown, click the new **Refresh** button, or change the Server URL.

---

## [2.4.1.1] - 2026-06-27

### Fixed
- 🐛 **npx/.cmd Launch on Windows** - External MCP servers started via `npx` (and other `.cmd`/`.bat` shims) now launch correctly. `Process.Start` cannot run `.cmd` files directly, so commands are resolved through `PATHEXT` and routed via `cmd.exe`. This unblocks most popular MCP servers (brave-search, context7, memory, sequential-thinking, etc.).
- 🐛 **BOM Handshake Corruption** - The stdio JSON-RPC stream no longer emits a UTF-8 BOM, which was breaking strict parsers (e.g. the Python MCP SDK) and preventing those servers from connecting.
- 🐛 **Concurrent Tool-Call Collisions** - The full request/response cycle is now serialized with a semaphore, fixing "the stream is in use by a previous operation" errors when tool calls overlap.

### Changed / Added
- 🧠 **External-Tools Prompt** - The MCP system prompt now instructs the model to use any available external tools (web fetch/search, filesystem, etc.) and to check its tools before refusing, instead of only NINA equipment tools.
- 🧩 **"Useful Servers" Picker** - Options now has a dropdown to add ready-made server configs (fetch, DuckDuckGo, Brave, Context7, memory, sequential-thinking, Wikipedia, arXiv, time, filesystem, everything) to the `mcpServers` JSON in one click.
- ⏳ **"AI is working" Indicator** - An animated indicator appears in the chat during processing so long tool-calling runs don't look frozen.
- 🖥️ **Ollama Remote Host** - Added guidance in Options for running the Ollama model on another LAN machine (`OLLAMA_HOST=0.0.0.0`, port 11434).

---

## [2.4.1.0] - 2026-06-27

### Added
- 🔌 **Multiple External MCP Servers** - Connect several third-party MCP servers simultaneously using the standard `mcpServers` JSON configuration (the same format used by Claude Desktop and VS Code). Works with Google Gemini and Ollama providers. (Issue #4)
- 🛠️ **Flexible Server Commands** - External MCP servers can now run any executable (python, node, npx, docker, or a binary) with custom `args` and `env` variables, instead of being limited to a single Python script.

### Changed
- Tool name collisions across external servers are automatically prefixed with the (sanitized) server name to avoid silent drops.
- Options UI replaces the single Python/script-path fields with an `mcpServers` JSON editor, a **Validate** button, and a **Reset to NINA Default** button that loads a prefilled NINA MCP server template.

### Fixed
- 🐛 **Connect/Disconnect All Accuracy** - `nina_connect_all_equipment` / `nina_disconnect_all_equipment` no longer report success based only on the HTTP status. They now read each device's actual API `Success` field and surface per-device error messages, so the AI no longer claims equipment is connected when it isn't.
- 🐛 **Multi-line Chat Paste** - The chat input box now preserves multi-line pasted text instead of truncating at the first line. Enter sends the message; Shift+Enter inserts a newline.

### Compatibility
- Backward compatible: existing single-server `ExternalMCPPythonPath` / `ExternalMCPScriptPath` settings are migrated automatically into one server config at runtime.

---

## [2.4.0.1] - 2026-06-27

### Fixed
- 🐛 **Mistral Model Dropdown Sync** - Selecting a model from the Mistral dropdown in Options now correctly syncs the model ID to the custom model textbox (missing entry in `ModelCombo_SelectionChanged` switch)
- 🐛 **Mistral Provider Display Name** - `GetProviderDisplayName` now returns "Mistral AI (Paid)" instead of falling through to the raw enum name "Mistral"

---

## [2.4.0.0] - 2026-06-24

### Added
- 🤖 **MCP Support for Ollama** - Local AI models can now control NINA equipment via MCP without paid API keys. Requires a tool-capable Ollama model (e.g., Llama 3.1+, Qwen 2.5+). Uses Ollama's native tool-calling API. (Closes #3)
- 🧠 **Mistral AI Provider** - New 6th AI provider supporting Mistral Large, Medium, Small, Open Mistral 7B, Open Mixtral 8x7B, and Codestral. Uses Mistral's OpenAI-compatible API with dynamic model discovery. (Closes #2)

### Changed
- MCP configuration section in Options now visible for Anthropic, Google, AND Ollama providers
- Provider count updated from 5 to 6
- Ollama timeout increased from 5 to 10 minutes to accommodate tool-calling iterations
- Ollama provider display name updated to reflect MCP capability

---

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
