using System.Reflection;
using System.Runtime.InteropServices;

// General Information
[assembly: AssemblyTitle("AI Assistant")]
[assembly: AssemblyDescription("Multi-provider AI assistant with MCP equipment control for intelligent astrophotography automation")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Michele Bergo")]
[assembly: AssemblyProduct("NINA.Plugins")]
[assembly: AssemblyCopyright("Copyright © 2026 Michele Bergo")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// COM visibility
[assembly: ComVisible(false)]

// Plugin GUID - Must match manifest.json Identifier
[assembly: Guid("af5e2826-e3b4-4b9c-9a1a-1e8d7c8b6a9e")]

// Version information
[assembly: AssemblyVersion("2.2.0.0")]
[assembly: AssemblyFileVersion("2.2.0.0")]

// Plugin metadata - aligned with NINA manifest standards
[assembly: AssemblyMetadata("Identifier", "AI Assistant")]
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
[assembly: AssemblyMetadata("Tags", "AI,Assistant,Chat,MCP,Automation,Image Analysis,GitHub Models")]

// Short description (required by NINA plugin manager)
[assembly: AssemblyMetadata("ShortDescription", "Multi-provider AI assistant with MCP equipment control, dynamic model discovery, image analysis, and extensible tool framework for intelligent astrophotography automation")]

// Long description
[assembly: AssemblyMetadata("LongDescription", @"Your intelligent astrophotography companion - transform NINA into a conversational, context-aware imaging system that understands your goals and helps you achieve better results.

🔭 FOR ASTROPHOTOGRAPHERS:
• Quick Session Setup: 'Set up for M31 tonight' - AI configures equipment, cooling, filters, and exposure settings
• Real-Time Troubleshooting: Analyze failed frames, high HFR, guiding issues, poor focus - get instant suggestions
• Image Quality Feedback: AI reviews your captures, identifies problems (tracking, focus, star bloat), suggests corrections
• Learning Assistant: Ask 'Why is my HFR high?' or 'Best Ha exposure for M42' - get expert guidance while imaging
• Sequence Optimization: 'Plan 4-hour session on Horsehead' - AI suggests optimal filter rotation, dither patterns, exposure times

🤖 5 AI PROVIDERS (Free to Advanced):
• GitHub Models (FREE) - No credit card, great for learning
• OpenAI GPT-4o/o1 - Most capable reasoning for complex planning
• Anthropic Claude Sonnet 4.5 - Best for equipment control via MCP
• Google Gemini 2.0 - Fast responses, MCP equipment control support
• Ollama (Local) - Privacy-focused, works offline, no API costs
Dynamic model discovery ensures you always have latest AI capabilities.

🎛️ NATURAL LANGUAGE EQUIPMENT CONTROL (via MCP):
Control your entire observatory through conversation:
• Camera: Take exposures, adjust cooling, bin settings, gain control
• Mount: GOTO coordinates, slew, park/unpark, tracking control
• Focuser: Move absolute/relative, run autofocus, temperature compensation
• Filter Wheel: Change filters, get positions, optimize filter rotation
• Guiding: Start/stop PHD2, dither, analyze drift
• Platesolving: Solve images, sync mount, analyze pointing accuracy
100+ built-in MCP tools for complete observatory control.

📊 IMAGE ANALYSIS:
• FITS Header Reading, Star Detection, HFR/FWHM monitoring
• Statistics Analysis, Quality Assessment with actionable recommendations
• Vision API Integration for advanced image understanding

🔌 EXTENSIBLE ARCHITECTURE:
• Built-in MCP Server via NINA Advanced API plugin
• External MCP Servers for community tools
• Dynamic model discovery and custom model IDs

Transform complex equipment control and imaging workflows into simple conversations.")]
