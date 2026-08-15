using System.Reflection;
using System.Runtime.InteropServices;

// General Information
[assembly: AssemblyTitle("AI Assistant")]
[assembly: AssemblyDescription("Multi-provider AI assistant with MCP equipment control for intelligent astrophotography automation")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Michele Bergo")]
[assembly: AssemblyProduct("NINA.Plugins")]
[assembly: AssemblyCopyright("Copyright Â© 2026 Michele Bergo")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// COM visibility
[assembly: ComVisible(false)]

// Plugin GUID - Must match manifest.json Identifier
[assembly: Guid("af5e2826-e3b4-4b9c-9a1a-1e8d7c8b6a9e")]

// Version information
[assembly: AssemblyVersion("2.5.2.0")]
[assembly: AssemblyFileVersion("2.5.2.0")]

// Plugin metadata - aligned with NINA manifest standards
[assembly: AssemblyMetadata("Identifier", "af5e2826-e3b4-4b9c-9a1a-1e8d7c8b6a9e")]
[assembly: AssemblyMetadata("Author", "Michele Bergo")]
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.0.0.0")]
[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
[assembly: AssemblyMetadata("Repository", "https://github.com/michelebergo/nina.plugin.aiassistant")]
[assembly: AssemblyMetadata("Homepage", "https://github.com/michelebergo/nina.plugin.aiassistant")]
[assembly: AssemblyMetadata("FeaturedImageURL", "https://raw.githubusercontent.com/michelebergo/nina.plugin.aiassistant/main/icon.png")]
[assembly: AssemblyMetadata("ScreenshotURL", "")]
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/michelebergo/nina.plugin.aiassistant/releases")]
[assembly: AssemblyMetadata("Tags", "AI,Assistant,Chat,MCP,Automation,Image Analysis,GitHub Models,Mistral,Ollama,Orchestrator")]

// Short description (required by NINA plugin manager)
[assembly: AssemblyMetadata("ShortDescription", "Multi-provider AI assistant with MCP equipment control, dynamic model discovery, image analysis, and extensible tool framework for intelligent astrophotography automation")]

// Long description
[assembly: AssemblyMetadata("LongDescription", @"Your intelligent astrophotography companion - transform NINA into a conversational, context-aware imaging system that understands your goals and helps you achieve better results.

ðŸ”­ FOR ASTROPHOTOGRAPHERS:
â€¢ Quick Session Setup: 'Set up for M31 tonight' - AI configures equipment, cooling, filters, and exposure settings
â€¢ Real-Time Troubleshooting: Analyze failed frames, high HFR, guiding issues, poor focus - get instant suggestions
â€¢ Image Quality Feedback: AI reviews your captures, identifies problems (tracking, focus, star bloat), suggests corrections
â€¢ Learning Assistant: Ask 'Why is my HFR high?' or 'Best Ha exposure for M42' - get expert guidance while imaging
â€¢ Sequence Optimization: 'Plan 4-hour session on Horsehead' - AI suggests optimal filter rotation, dither patterns, exposure times

ðŸ¤– 5 AI PROVIDERS (Free to Advanced):
â€¢ Ollama (Local) - Privacy-focused, works offline, no API costs, no key
â€¢ Google Gemini - Free API tier, fast responses, MCP equipment control support
â€¢ Mistral AI - Free API tier, European-hosted models for chat, reasoning and coding
â€¢ OpenAI GPT - Most capable reasoning for complex planning
â€¢ Anthropic Claude - Best for equipment control via MCP
Dynamic model discovery ensures you always have latest AI capabilities.
(GitHub Models was retired by GitHub on July 30, 2026 and no longer works.)

ðŸŽ›ï¸ NATURAL LANGUAGE EQUIPMENT CONTROL (via MCP):
Control your entire observatory through conversation:
â€¢ Camera: Take exposures, adjust cooling, bin settings, gain control
â€¢ Mount: GOTO coordinates, slew, park/unpark, tracking control
â€¢ Focuser: Move absolute/relative, run autofocus, temperature compensation
â€¢ Filter Wheel: Change filters, get positions, optimize filter rotation
â€¢ Guiding: Start/stop PHD2, dither, analyze drift
â€¢ Platesolving: Solve images, sync mount, analyze pointing accuracy
100+ built-in MCP tools for complete observatory control.

ðŸ“Š IMAGE ANALYSIS:
â€¢ FITS Header Reading, Star Detection, HFR/FWHM monitoring
â€¢ Statistics Analysis, Quality Assessment with actionable recommendations
â€¢ Vision API Integration for advanced image understanding

ðŸ”Œ EXTENSIBLE ARCHITECTURE:
â€¢ Built-in MCP Server via NINA Advanced API plugin
â€¢ External MCP Servers for community tools
â€¢ Dynamic model discovery and custom model IDs
â€¢ Optional nina.autopilot Orchestrator dashboard integration

Transform complex equipment control and imaging workflows into simple conversations.")]

