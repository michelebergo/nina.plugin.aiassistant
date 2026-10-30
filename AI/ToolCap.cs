using System;
using System.Collections.Generic;
using System.Linq;
using NINA.Core.Utility;
using NINA.Plugin.AIAssistant.AI.MCP;

namespace NINA.Plugin.AIAssistant.AI
{
    /// <summary>
    /// Keeps a tool list under a provider's hard limit. OpenAI accepts at most 128 tools per
    /// request and the bridge declares more; rather than failing the whole conversation, the
    /// least conversational groups are dropped first - rescans, device listings, one-by-one
    /// disconnects, readout variants, park positions - until the list fits. Tools the system
    /// prompt names, the wiki tools and anything from an external server are never dropped.
    /// </summary>
    public static class ToolCap
    {
        /// <summary>
        /// Dropped in this order, group by group, until the list fits. A group is a predicate
        /// on the tool name.
        /// </summary>
        private static readonly (string Label, Func<string, bool> Matches)[] LowPriorityGroups =
        {
            ("rescans",              n => n.StartsWith("nina_rescan_")),
            ("device listings",      n => n.StartsWith("nina_list_") && n.EndsWith("_devices") || n == "nina_list_weather_sources"),
            ("single disconnects",   n => n.StartsWith("nina_disconnect_") && n != "nina_disconnect_all_equipment"),
            ("readout variants",     n => n.StartsWith("nina_set_camera_readout_") || n == "nina_set_camera_usb_limit"),
            ("park positions",       n => n.EndsWith("_park_position")),
            ("rotator mechanics",    n => n.StartsWith("nina_move_rotator_mechanical") || n == "nina_set_rotator_mechanical_range" || n == "nina_set_rotator_reverse" || n == "nina_halt_rotator"),
            ("dome details",         n => n == "nina_sync_dome" || n == "nina_set_dome_follow" || n == "nina_home_dome" || n == "nina_stop_dome"),
            ("trained flats",        n => n.StartsWith("nina_trained_") || n == "nina_auto_brightness_flat" || n == "nina_auto_exposure_flat"),
            ("framing setters",      n => n.StartsWith("nina_set_framingassistant_") || n == "nina_determine_framingassistant_rotation"),
            ("filter list editing",  n => n == "nina_add_filter" || n == "nina_remove_filter"),
            ("ui and plugin pages",  n => n == "nina_switch_tab" || n == "nina_get_tab" || n == "nina_get_plugins" || n == "nina_get_plugin_settings"),
            ("duplicates and trivia", n => n == "nina_get_nina_version" || n == "nina_get_start_time"),
            ("sequence json",        n => n == "nina_sequence_json" || n == "nina_sequence_edit"),
        };

        private static readonly HashSet<string> Protected = new(StringComparer.Ordinal)
        {
            "nina_get_status", "nina_connect_all_equipment", "nina_disconnect_all_equipment",
            "nina_capture_image", "nina_slew_mount", "nina_park_mount", "nina_show_profile",
            "nina_get_version", "nina_connect_camera", "nina_connect_mount"
        };

        public static bool IsInALowPriorityGroup(string name) => LowPriorityGroups.Any(g => g.Matches(name));

        private static bool IsBuiltIn(string name) => name.StartsWith("nina_");
        private static bool IsProtected(string name) => Protected.Contains(name) || name.StartsWith("wiki_") || !IsBuiltIn(name);

        public static List<MCPTool> Apply(IReadOnlyList<MCPTool> tools, int max, out List<string> dropped)
        {
            dropped = new List<string>();
            if (tools.Count <= max) return tools.ToList();

            var kept = tools.ToList();

            foreach (var group in LowPriorityGroups)
            {
                if (kept.Count <= max) break;
                var victims = kept.Where(t => !IsProtected(t.Name) && group.Matches(t.Name)).ToList();
                if (victims.Count == 0) continue;
                kept.RemoveAll(victims.Contains);
                dropped.AddRange(victims.Select(v => v.Name));
                Logger.Info($"ToolCap: dropped {victims.Count} {group.Label} to fit {max} tools");
            }

            // Last resort: trim unprotected built-ins from the end of the list. The shipped
            // catalogue alone never gets here (a test guards that); a user's external servers
            // take protected seats, so with enough of them this is the normal path.
            if (kept.Count > max)
            {
                var tail = new List<string>();
                for (int i = kept.Count - 1; i >= 0 && kept.Count > max; i--)
                {
                    if (IsProtected(kept[i].Name)) continue;
                    tail.Add(kept[i].Name);
                    kept.RemoveAt(i);
                }
                dropped.AddRange(tail);
                Logger.Info($"ToolCap: external tools take {kept.Count(t => !IsBuiltIn(t.Name))} of the {max} seats; also left out from the end: {string.Join(", ", tail)}");
            }

            return kept;
        }
    }
}
