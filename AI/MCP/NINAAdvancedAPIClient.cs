using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NINA.Core.Utility;

namespace NINA.Plugin.AIAssistant.AI.MCP
{
    /// <summary>
    /// Client for directly calling NINA Advanced API endpoints
    /// This provides the same functionality as the MCP server but directly via HTTP
    /// </summary>
    public class NINAAdvancedAPIClient
    {
        private readonly HttpClient _httpClient;
        private MCPConfig? _config;
        private bool _isConnected;

        public NINAAdvancedAPIClient()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(300); // Long timeout for captures + plate solving
        }

        public bool IsConnected => _isConnected;

        /// <summary>
        /// Initialize the client with configuration
        /// </summary>
        public async Task<bool> InitializeAsync(MCPConfig config, CancellationToken cancellationToken = default)
        {
            try
            {
                _config = config;
                var baseUrl = $"http://{config.NinaHost}:{config.NinaPort}/v2/api";
                
                // Test connection
                var response = await _httpClient.GetAsync($"{baseUrl}/version", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    _isConnected = true;
                    Logger.Info($"NINA Advanced API connected at {config.NinaHost}:{config.NinaPort}");
                    return true;
                }
                else
                {
                    Logger.Warning($"NINA Advanced API connection failed: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to connect to NINA Advanced API: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the list of available tools with their definitions
        /// </summary>
        public List<MCPTool> GetAvailableTools()
        {
            return new List<MCPTool>
            {
                // Equipment Status & Connection
                new MCPTool
                {
                    Name = "nina_get_status",
                    Description = "Get the current status of all connected equipment in NINA (camera, mount, focuser, filter wheel, guider, dome, etc.)",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_version",
                    Description = "Get the version of the NINA Advanced API plugin",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_nina_version",
                    Description = "Get the version of NINA itself",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["friendly"] = new MCPToolParameter { Type = "boolean", Description = "If true, returns version in a friendly format (e.g., '3.2 NIGHTLY #058' instead of '3.2.0.1058')" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_get_start_time",
                    Description = "Get the time NINA was started",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_connect_all_equipment",
                    Description = "Connect to all configured equipment in NINA at once (Camera, Mount, Focuser, Filter Wheel, Guider, Dome, Rotator, Flat Panel, Switch, Weather, Safety Monitor)",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_disconnect_all_equipment",
                    Description = "Disconnect from all configured equipment in NINA at once",
                    InputSchema = new MCPToolInputSchema()
                },

                // Camera Operations
                new MCPTool
                {
                    Name = "nina_connect_camera",
                    Description = "Connect to a camera device in NINA",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["device_id"] = new MCPToolParameter { Type = "string", Description = "Optional device ID to connect to" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_disconnect_camera",
                    Description = "Disconnect the camera from NINA",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_camera_info",
                    Description = "Get detailed information about the connected camera",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_capture_image",
                    Description = "Capture an image with the camera",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["duration"] = new MCPToolParameter { Type = "number", Description = "Exposure time in seconds" },
                            ["gain"] = new MCPToolParameter { Type = "integer", Description = "Camera gain setting" },
                            ["download"] = new MCPToolParameter { Type = "boolean", Description = "Whether to download the image data" },
                            ["solve"] = new MCPToolParameter { Type = "boolean", Description = "Whether to plate solve the image after capture. The call will wait for the solve to complete and return the solve results." }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_rescan_camera",
                    Description = "Rescan for camera devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_list_camera_devices",
                    Description = "List available camera devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_set_camera_readout",
                    Description = "Set the camera readout mode",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["mode"] = new MCPToolParameter { Type = "integer", Description = "Readout mode index" }
                        },
                        Required = new List<string> { "mode" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_set_camera_readout_image",
                    Description = "Set the camera readout mode for imaging",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["mode"] = new MCPToolParameter { Type = "integer", Description = "Readout mode index" }
                        },
                        Required = new List<string> { "mode" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_set_camera_readout_snapshot",
                    Description = "Set the camera readout mode for snapshots",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["mode"] = new MCPToolParameter { Type = "integer", Description = "Readout mode index" }
                        },
                        Required = new List<string> { "mode" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_set_camera_usb_limit",
                    Description = "Set the camera USB limit",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["limit"] = new MCPToolParameter { Type = "integer", Description = "USB Limit value" }
                        },
                        Required = new List<string> { "limit" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_start_cooling",
                    Description = "Start cooling the camera to a target temperature",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["temperature"] = new MCPToolParameter { Type = "number", Description = "Target temperature in Celsius" },
                            ["duration"] = new MCPToolParameter { Type = "integer", Description = "Duration in minutes" }
                        },
                        Required = new List<string> { "temperature" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_stop_cooling",
                    Description = "Stop the camera cooling process",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_abort_exposure",
                    Description = "Abort the current camera exposure",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_capture_statistics",
                    Description = "Get statistics about the last captured image (stars, HFR, median, etc.)",
                    InputSchema = new MCPToolInputSchema()
                },

                // Mount Operations
                new MCPTool
                {
                    Name = "nina_connect_mount",
                    Description = "Connect to a mount device in NINA",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["device_id"] = new MCPToolParameter { Type = "string", Description = "Optional device ID to connect to" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_disconnect_mount",
                    Description = "Disconnect the mount from NINA",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_mount_info",
                    Description = "Get detailed information about the connected mount",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_rescan_mount",
                    Description = "Rescan for mount devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_list_mount_devices",
                    Description = "List all available mount devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_slew_mount",
                    Description = "Slew the mount to specified coordinates. Can optionally center on target with plate solving or rotate to match a framing angle.",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["ra"] = new MCPToolParameter { Type = "number", Description = "Right Ascension in hours (0-24)" },
                            ["dec"] = new MCPToolParameter { Type = "number", Description = "Declination in degrees (-90 to +90)" },
                            ["waitForResult"] = new MCPToolParameter { Type = "boolean", Description = "Whether to wait for slew to complete (default: true)" },
                            ["center"] = new MCPToolParameter { Type = "boolean", Description = "Whether to center on the target using plate solving after slew" },
                            ["rotate"] = new MCPToolParameter { Type = "boolean", Description = "Whether to perform a center and rotate to match a specific rotation angle" },
                            ["rotationAngle"] = new MCPToolParameter { Type = "number", Description = "The rotation angle in degrees (used with rotate=true)" }
                        },
                        Required = new List<string> { "ra", "dec" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_park_mount",
                    Description = "Park the mount",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_unpark_mount",
                    Description = "Unpark the mount",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_set_mount_park_position",
                    Description = "Set the current mount position as the park position. Requires mount to be unparked.",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_home_mount",
                    Description = "Send the mount to its home position",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_stop_slew",
                    Description = "Stop the mount's current slew. Best for simple slews without center/rotate. With center or rotate, may take a few seconds.",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_flip_mount",
                    Description = "Perform a meridian flip. Only flips if needed based on current pier side, will not force the mount to flip.",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_sync_mount",
                    Description = "Sync the mount. If RA/Dec coordinates are provided, syncs to those coordinates. If omitted, performs a plate solve and syncs to solved coordinates.",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["ra"] = new MCPToolParameter { Type = "number", Description = "Right Ascension in hours (0-24). If omitted, a plate solve will be performed." },
                            ["dec"] = new MCPToolParameter { Type = "number", Description = "Declination in degrees (-90 to +90). If omitted, a plate solve will be performed." }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_set_tracking_mode",
                    Description = "Set the mount's tracking mode",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["mode"] = new MCPToolParameter 
                            { 
                                Type = "string", 
                                Description = "Tracking mode: SIDEREAL, LUNAR, SOLAR, KING, or STOPPED",
                                Enum = new List<string> { "SIDEREAL", "LUNAR", "SOLAR", "KING", "STOPPED" }
                            }
                        },
                        Required = new List<string> { "mode" }
                    }
                },

                // Focuser Operations
                new MCPTool
                {
                    Name = "nina_connect_focuser",
                    Description = "Connect to a focuser device in NINA",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["device_id"] = new MCPToolParameter { Type = "string", Description = "Optional device ID to connect to" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_disconnect_focuser",
                    Description = "Disconnect the focuser from NINA",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_focuser_info",
                    Description = "Get information about the connected focuser",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_list_focuser_devices",
                    Description = "List all available focuser devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_rescan_focuser",
                    Description = "Rescan for focuser devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_move_focuser",
                    Description = "Move the focuser to a specific absolute position in steps",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["position"] = new MCPToolParameter { Type = "integer", Description = "Target absolute position in steps" }
                        },
                        Required = new List<string> { "position" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_stop_focuser_move",
                    Description = "Stop the current focuser movement",
                    InputSchema = new MCPToolInputSchema()
                },

                // Filter Wheel Operations
                new MCPTool
                {
                    Name = "nina_connect_filterwheel",
                    Description = "Connect to a filter wheel in NINA",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["device_id"] = new MCPToolParameter { Type = "string", Description = "Optional device ID to connect to" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_disconnect_filterwheel",
                    Description = "Disconnect the filter wheel from NINA",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_filterwheel_info",
                    Description = "Get information about the connected filter wheel",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_rescan_filterwheel",
                    Description = "Rescan for filter wheel devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_list_filterwheel_devices",
                    Description = "List available filter wheel devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_change_filter",
                    Description = "Change to a specific filter",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["filter_id"] = new MCPToolParameter { Type = "integer", Description = "ID of the filter to change to" }
                        },
                        Required = new List<string> { "filter_id" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_add_filter",
                    Description = "Add a new filter slot to the filter wheel configuration",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_remove_filter",
                    Description = "Remove a filter by its filter ID",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["filter_id"] = new MCPToolParameter { Type = "integer", Description = "Filter ID to remove" }
                        },
                        Required = new List<string> { "filter_id" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_get_filter_info",
                    Description = "Get details of a filter by its filter ID",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["filter_id"] = new MCPToolParameter { Type = "integer", Description = "Filter ID" }
                        },
                        Required = new List<string> { "filter_id" }
                    }
                },

                // Guider Operations
                new MCPTool
                {
                    Name = "nina_connect_guider",
                    Description = "Connect to a guider (like PHD2) in NINA",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["device_id"] = new MCPToolParameter { Type = "string", Description = "Optional device ID to connect to" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_disconnect_guider",
                    Description = "Disconnect the guider from NINA",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_guider_info",
                    Description = "Get information about the connected guider",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_rescan_guider",
                    Description = "Rescan for guider devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_list_guider_devices",
                    Description = "List available guider devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_start_guiding",
                    Description = "Start guiding with optional calibration",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["calibrate"] = new MCPToolParameter { Type = "boolean", Description = "Whether to calibrate before guiding" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_stop_guiding",
                    Description = "Stop guiding",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_clear_guider_calibration",
                    Description = "Clear the guider calibration",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_guider_graph",
                    Description = "Get the guider graph data (RMS, guide pulses, etc.)",
                    InputSchema = new MCPToolInputSchema()
                },

                // Dome Operations
                new MCPTool
                {
                    Name = "nina_connect_dome",
                    Description = "Connect to a dome device in NINA",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["device_id"] = new MCPToolParameter { Type = "string", Description = "Optional device ID to connect to" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_disconnect_dome",
                    Description = "Disconnect the dome from NINA",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_dome_info",
                    Description = "Get information about the connected dome",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_rescan_dome",
                    Description = "Rescan for dome devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_list_dome_devices",
                    Description = "List available dome devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_home_dome",
                    Description = "Send the dome to its home position",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_stop_dome",
                    Description = "Stop dome movement",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_set_dome_follow",
                    Description = "Enable or disable dome following",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["follow"] = new MCPToolParameter { Type = "boolean", Description = "True to follow" }
                        },
                        Required = new List<string> { "follow" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_sync_dome",
                    Description = "Sync dome position with the current mount position",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_set_dome_park_position",
                    Description = "Set the current dome position as the park position",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_open_dome_shutter",
                    Description = "Open the dome shutter",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_close_dome_shutter",
                    Description = "Close the dome shutter",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_park_dome",
                    Description = "Park the dome",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_slew_dome",
                    Description = "Slew the dome to a specific azimuth",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["azimuth"] = new MCPToolParameter { Type = "number", Description = "Target azimuth in degrees" },
                            ["waitToFinish"] = new MCPToolParameter { Type = "boolean", Description = "Whether to wait for the dome to finish slewing (default: false)" }
                        },
                        Required = new List<string> { "azimuth" }
                    }
                },

                // Image History
                new MCPTool
                {
                    Name = "nina_get_image_history",
                    Description = "Get the history of captured images",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["all"] = new MCPToolParameter { Type = "boolean", Description = "Return all entries (default: true)" },
                            ["index"] = new MCPToolParameter { Type = "integer", Description = "Starting index" },
                            ["count"] = new MCPToolParameter { Type = "integer", Description = "Number of entries to return" },
                            ["imageType"] = new MCPToolParameter
                            {
                                Type = "string",
                                Description = "Filter by image type",
                                Enum = new List<string> { "LIGHT", "DARK", "BIAS", "FLAT", "SNAPSHOT" }
                            }
                        }
                    }
                },

                // Application
                new MCPTool
                {
                    Name = "nina_switch_tab",
                    Description = "Switch to a specific tab in NINA",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["tab"] = new MCPToolParameter
                            {
                                Type = "string",
                                Description = "Name of the tab to switch to",
                                Enum = new List<string> { "equipment", "skyatlas", "framing", "flatwizard", "sequencer", "imaging", "options" }
                            }
                        },
                        Required = new List<string> { "tab" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_get_tab",
                    Description = "Get the current active tab in NINA",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_plugins",
                    Description = "Get a list of installed NINA plugins",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_plugin_settings",
                    Description = "Get the Advanced API plugin settings (e.g., AccessControlHeaderEnabled, ShouldCreateThumbnails)",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_logs",
                    Description = "Get the last N log entries from NINA. Useful for diagnosing issues.",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["lineCount"] = new MCPToolParameter { Type = "integer", Description = "Number of log lines to return" },
                            ["level"] = new MCPToolParameter
                            {
                                Type = "string",
                                Description = "Minimum log level filter",
                                Enum = new List<string> { "TRACE", "DEBUG", "INFO", "WARNING", "ERROR" }
                            }
                        },
                        Required = new List<string> { "lineCount" }
                    }
                },

                // Flats
                new MCPTool
                {
                    Name = "nina_skyflat",
                    Description = "Capture sky flat frames with automatic exposure settings",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["count"] = new MCPToolParameter { Type = "integer", Description = "Number of flats to capture" },
                            ["minExposure"] = new MCPToolParameter { Type = "number", Description = "Minimum exposure time in seconds" },
                            ["maxExposure"] = new MCPToolParameter { Type = "number", Description = "Maximum exposure time in seconds" },
                            ["histogramMean"] = new MCPToolParameter { Type = "number", Description = "Target histogram mean (0-1)" },
                            ["meanTolerance"] = new MCPToolParameter { Type = "number", Description = "Tolerance for histogram mean" },
                            ["dither"] = new MCPToolParameter { Type = "boolean", Description = "Whether to dither between flats" },
                            ["filterId"] = new MCPToolParameter { Type = "integer", Description = "Filter ID to use" },
                            ["binning"] = new MCPToolParameter { Type = "string", Description = "Binning mode (e.g., 1x1, 2x2)" },
                            ["gain"] = new MCPToolParameter { Type = "integer", Description = "Camera gain" },
                            ["offset"] = new MCPToolParameter { Type = "integer", Description = "Camera offset" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_auto_brightness_flat",
                    Description = "Capture flat frames with automatic brightness adjustment (for flat panels)",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["count"] = new MCPToolParameter { Type = "integer", Description = "Number of flats to capture" },
                            ["exposureTime"] = new MCPToolParameter { Type = "number", Description = "Exposure time in seconds" },
                            ["minBrightness"] = new MCPToolParameter { Type = "integer", Description = "Minimum panel brightness (0-100)" },
                            ["maxBrightness"] = new MCPToolParameter { Type = "integer", Description = "Maximum panel brightness (0-100)" },
                            ["histogramMean"] = new MCPToolParameter { Type = "number", Description = "Target histogram mean (0-1)" },
                            ["meanTolerance"] = new MCPToolParameter { Type = "number", Description = "Tolerance for histogram mean" },
                            ["filterId"] = new MCPToolParameter { Type = "integer", Description = "Filter ID to use" },
                            ["binning"] = new MCPToolParameter { Type = "string", Description = "Binning mode (e.g., 1x1, 2x2)" },
                            ["gain"] = new MCPToolParameter { Type = "integer", Description = "Camera gain" },
                            ["offset"] = new MCPToolParameter { Type = "integer", Description = "Camera offset" },
                            ["keepClosed"] = new MCPToolParameter { Type = "boolean", Description = "Keep flat panel cover closed" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_auto_exposure_flat",
                    Description = "Capture flat frames with automatic exposure adjustment (for flat panels)",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["count"] = new MCPToolParameter { Type = "integer", Description = "Number of flats to capture" },
                            ["brightness"] = new MCPToolParameter { Type = "integer", Description = "Panel brightness (0-100)" },
                            ["minExposure"] = new MCPToolParameter { Type = "number", Description = "Minimum exposure time in seconds" },
                            ["maxExposure"] = new MCPToolParameter { Type = "number", Description = "Maximum exposure time in seconds" },
                            ["histogramMean"] = new MCPToolParameter { Type = "number", Description = "Target histogram mean (0-1)" },
                            ["meanTolerance"] = new MCPToolParameter { Type = "number", Description = "Tolerance for histogram mean" },
                            ["filterId"] = new MCPToolParameter { Type = "integer", Description = "Filter ID to use" },
                            ["binning"] = new MCPToolParameter { Type = "string", Description = "Binning mode (e.g., 1x1, 2x2)" },
                            ["gain"] = new MCPToolParameter { Type = "integer", Description = "Camera gain" },
                            ["offset"] = new MCPToolParameter { Type = "integer", Description = "Camera offset" },
                            ["keepClosed"] = new MCPToolParameter { Type = "boolean", Description = "Keep flat panel cover closed" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_trained_dark_flat",
                    Description = "Capture trained dark flat frames using saved exposure settings",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["count"] = new MCPToolParameter { Type = "integer", Description = "Number of flats to capture" },
                            ["filterId"] = new MCPToolParameter { Type = "integer", Description = "Filter ID to use" },
                            ["binning"] = new MCPToolParameter { Type = "string", Description = "Binning mode (e.g., 1x1, 2x2)" },
                            ["gain"] = new MCPToolParameter { Type = "integer", Description = "Camera gain" },
                            ["offset"] = new MCPToolParameter { Type = "integer", Description = "Camera offset" },
                            ["keepClosed"] = new MCPToolParameter { Type = "boolean", Description = "Keep flat panel cover closed" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_trained_flat",
                    Description = "Capture trained flat frames using saved exposure settings",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["count"] = new MCPToolParameter { Type = "integer", Description = "Number of flats to capture" },
                            ["filterId"] = new MCPToolParameter { Type = "integer", Description = "Filter ID to use" },
                            ["binning"] = new MCPToolParameter { Type = "string", Description = "Binning mode (e.g., 1x1, 2x2)" },
                            ["gain"] = new MCPToolParameter { Type = "integer", Description = "Camera gain" },
                            ["offset"] = new MCPToolParameter { Type = "integer", Description = "Camera offset" },
                            ["keepClosed"] = new MCPToolParameter { Type = "boolean", Description = "Keep flat panel cover closed" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_stop_flats",
                    Description = "Stop capturing flat frames",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_flats_status",
                    Description = "Get the current status of flat frame capturing",
                    InputSchema = new MCPToolInputSchema()
                },

                // Rotator Operations
                new MCPTool
                {
                    Name = "nina_connect_rotator",
                    Description = "Connect to a rotator device in NINA",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["device_id"] = new MCPToolParameter { Type = "string", Description = "Optional device ID to connect to" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_disconnect_rotator",
                    Description = "Disconnect the rotator from NINA",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_list_rotator_devices",
                    Description = "List available rotator devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_rescan_rotator",
                    Description = "Rescan for rotator devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_rotator_info",
                    Description = "Get information about the connected rotator",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_move_rotator",
                    Description = "Move the rotator to a specific position in degrees",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["position"] = new MCPToolParameter { Type = "number", Description = "Target position in degrees" }
                        },
                        Required = new List<string> { "position" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_move_rotator_mechanical",
                    Description = "Move the rotator safely avoiding wrap-around",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["position"] = new MCPToolParameter { Type = "number", Description = "Target mechanical position" }
                        },
                        Required = new List<string> { "position" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_set_rotator_mechanical_range",
                    Description = "Set mechanical range constraint for the rotator",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["range"] = new MCPToolParameter
                            {
                                Type = "string",
                                Description = "Range type for the rotator",
                                Enum = new List<string> { "full", "half", "quarter" }
                            },
                            ["rangeStartPosition"] = new MCPToolParameter { Type = "number", Description = "Starting position for the range" }
                        },
                        Required = new List<string> { "range" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_halt_rotator",
                    Description = "Halt the rotator's current movement",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_set_rotator_reverse",
                    Description = "Set the rotator's reverse direction state",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["enabled"] = new MCPToolParameter { Type = "boolean", Description = "True to enable reverse direction, False to disable" }
                        },
                        Required = new List<string> { "enabled" }
                    }
                },

                // Flat Panel Operations
                new MCPTool
                {
                    Name = "nina_connect_flatpanel",
                    Description = "Connect to a flat panel device in NINA",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["device_id"] = new MCPToolParameter { Type = "string", Description = "Optional device ID to connect to" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_disconnect_flatpanel",
                    Description = "Disconnect the flat panel from NINA",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_list_flatpanel_devices",
                    Description = "List available flat panel devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_rescan_flatpanel",
                    Description = "Rescan for flat panel devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_flatpanel_info",
                    Description = "Get information about the connected flat panel",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_set_flatpanel_light",
                    Description = "Turn the flat panel light on or off",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["on"] = new MCPToolParameter { Type = "boolean", Description = "True to turn on, False to turn off" }
                        },
                        Required = new List<string> { "on" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_set_flatpanel_cover",
                    Description = "Set the flat panel cover state",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["closed"] = new MCPToolParameter { Type = "boolean", Description = "True to close, False to open" }
                        },
                        Required = new List<string> { "closed" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_set_flatpanel_brightness",
                    Description = "Set the flat panel brightness",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["brightness"] = new MCPToolParameter { Type = "integer", Description = "Brightness value (0-100)" }
                        },
                        Required = new List<string> { "brightness" }
                    }
                },

                // Switch Operations
                new MCPTool
                {
                    Name = "nina_connect_switch",
                    Description = "Connect to a switch device in NINA",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["device_id"] = new MCPToolParameter { Type = "string", Description = "Device ID to connect to" }
                        },
                        Required = new List<string> { "device_id" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_disconnect_switch",
                    Description = "Disconnect the switch from NINA",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_list_switch_devices",
                    Description = "List available switch devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_rescan_switch",
                    Description = "Rescan for switch devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_switch_channels",
                    Description = "Get all writable and read-only channels from the connected switch",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_set_switch",
                    Description = "Set a writable switch channel by index",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["index"] = new MCPToolParameter { Type = "integer", Description = "Writable channel index" },
                            ["value"] = new MCPToolParameter { Type = "number", Description = "Target value (0/1 for binary or analog value)" }
                        },
                        Required = new List<string> { "index", "value" }
                    }
                },

                // Weather Station Operations
                new MCPTool
                {
                    Name = "nina_connect_weather",
                    Description = "Connect to a weather station in NINA",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["device_id"] = new MCPToolParameter { Type = "string", Description = "Optional device ID to connect to" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_disconnect_weather",
                    Description = "Disconnect the weather station from NINA",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_weather_info",
                    Description = "Get comprehensive weather information (temperature, humidity, pressure, wind, etc.)",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_list_weather_sources",
                    Description = "List all available weather station sources",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_rescan_weather",
                    Description = "Rescan for weather sources",
                    InputSchema = new MCPToolInputSchema()
                },

                // Safety Monitor Operations
                new MCPTool
                {
                    Name = "nina_connect_safetymonitor",
                    Description = "Connect to a safety monitor in NINA",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["device_id"] = new MCPToolParameter { Type = "string", Description = "Optional device ID to connect to" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_disconnect_safetymonitor",
                    Description = "Disconnect the safety monitor from NINA",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_safetymonitor_info",
                    Description = "Get safety monitor information and current safety status",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_list_safetymonitor_devices",
                    Description = "List all available safety monitor devices",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_rescan_safetymonitor",
                    Description = "Rescan for safety monitor devices",
                    InputSchema = new MCPToolInputSchema()
                },

                // Advanced Camera Operations
                new MCPTool
                {
                    Name = "nina_set_binning",
                    Description = "Set the camera's binning mode",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["binning"] = new MCPToolParameter { Type = "string", Description = "Binning mode (e.g., 1x1, 2x2, 3x3, 4x4)" }
                        },
                        Required = new List<string> { "binning" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_control_dew_heater",
                    Description = "Control the camera's dew heater",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["power"] = new MCPToolParameter { Type = "boolean", Description = "True to enable, False to disable" }
                        },
                        Required = new List<string> { "power" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_start_warming",
                    Description = "Start warming the camera",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["duration"] = new MCPToolParameter { Type = "integer", Description = "Duration in minutes" }
                        }
                    }
                },

                // Advanced Focuser Operations
                new MCPTool
                {
                    Name = "nina_start_autofocus",
                    Description = "Start an autofocus routine using the configured autofocus method",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_cancel_autofocus",
                    Description = "Cancel the current autofocus routine",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_last_autofocus",
                    Description = "Get the result of the last autofocus routine",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_halt_focuser",
                    Description = "Halt the focuser's current movement",
                    InputSchema = new MCPToolInputSchema()
                },

                // Sequence Operations
                new MCPTool
                {
                    Name = "nina_sequence_start",
                    Description = "Start the Advanced Sequence in NINA",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["skipValidation"] = new MCPToolParameter { Type = "boolean", Description = "Whether to skip sequence validation" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_sequence_stop",
                    Description = "Stop the Advanced Sequence",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_sequence_load",
                    Description = "Load a sequence from a file",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["sequenceName"] = new MCPToolParameter { Type = "string", Description = "Name of the sequence to load" }
                        },
                        Required = new List<string> { "sequenceName" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_sequence_json",
                    Description = "Get the current sequence as JSON",
                    InputSchema = new MCPToolInputSchema()
                },

                // Plate Solving Operations
                new MCPTool
                {
                    Name = "nina_platesolve_capsolve",
                    Description = "Plate solve the currently loaded image",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["blind"] = new MCPToolParameter { Type = "boolean", Description = "Whether to use blind solving" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_platesolve_sync",
                    Description = "Plate solve the current image and sync the mount",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["blind"] = new MCPToolParameter { Type = "boolean", Description = "Whether to use blind solving" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_platesolve_center",
                    Description = "Center on target coordinates using plate solving",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["ra"] = new MCPToolParameter { Type = "number", Description = "Right Ascension in hours (0-24)" },
                            ["dec"] = new MCPToolParameter { Type = "number", Description = "Declination in degrees (-90 to +90)" }
                        },
                        Required = new List<string> { "ra", "dec" }
                    }
                },

                // Framing Assistant Operations
                // Framing Assistant Operations
                new MCPTool
                {
                    Name = "nina_get_framingassistant_info",
                    Description = "Get information about the current framing assistant state (coordinates, field of view, rotation, panel layout, etc.)",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_set_framingassistant_source",
                    Description = "Set the sky survey image source for the framing assistant. The framing assistant must be initialized first (opened once in the UI).",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["source"] = new MCPToolParameter
                            {
                                Type = "string",
                                Description = "Sky survey source to use for framing images",
                                Enum = new List<string> { "NASA", "SKYSERVER", "STSCI", "ESO", "HIPS2FITS", "SKYATLAS", "FILE", "CACHE" }
                            }
                        },
                        Required = new List<string> { "source" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_set_framingassistant_coordinates",
                    Description = "Set the framing assistant target coordinates. Use your knowledge of deep sky object coordinates (e.g., M31 = RA 0.712h, Dec 41.27°; M42 = RA 5.588h, Dec -5.39°). The framing assistant must be initialized first (opened once in the UI).",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["ra"] = new MCPToolParameter { Type = "number", Description = "Right Ascension in hours (0-24)" },
                            ["dec"] = new MCPToolParameter { Type = "number", Description = "Declination in degrees (-90 to +90)" }
                        },
                        Required = new List<string> { "ra", "dec" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_set_framingassistant_rotation",
                    Description = "Set the framing assistant rotation angle. The framing assistant must be initialized first (opened once in the UI).",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["rotation"] = new MCPToolParameter { Type = "number", Description = "Rotation angle in degrees" }
                        },
                        Required = new List<string> { "rotation" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_determine_framingassistant_rotation",
                    Description = "Determine the framing assistant rotation from the camera. Requires an image to be loaded in the framing assistant. The framing assistant must be initialized first.",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["waitForResult"] = new MCPToolParameter { Type = "boolean", Description = "Whether to wait for the rotation determination to complete (recommended: true)" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_framingassistant_slew",
                    Description = "Slew the mount to the framing assistant target coordinates. The framing assistant must be initialized first (opened once in the UI).",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["slew_option"] = new MCPToolParameter
                            {
                                Type = "string",
                                Description = "Slew option: Center (center on target with plate solving), Rotate (rotate to match framing angle), or omit for simple slew",
                                Enum = new List<string> { "Center", "Rotate" }
                            },
                            ["waitForResult"] = new MCPToolParameter { Type = "boolean", Description = "Whether to wait for the slew to finish" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_moon_separation",
                    Description = "Calculate the moon separation angle for given coordinates at the current time and location. Useful for planning observations.",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["ra"] = new MCPToolParameter { Type = "number", Description = "Right Ascension in hours (0-24)" },
                            ["dec"] = new MCPToolParameter { Type = "number", Description = "Declination in degrees (-90 to +90)" }
                        },
                        Required = new List<string> { "ra", "dec" }
                    }
                },

                // Profile Operations
                new MCPTool
                {
                    Name = "nina_show_profile",
                    Description = "Show profile information. Use active=true to see only the active profile, or active=false to see all profiles.",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["active"] = new MCPToolParameter { Type = "boolean", Description = "If true, show only the active profile (default: false shows all profiles)" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_change_profile_value",
                    Description = "Change a profile setting value by its path (e.g., 'CameraSettings.PixelSize', 'AstrometrySettings.Latitude')",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["settingpath"] = new MCPToolParameter { Type = "string", Description = "Setting path (e.g., CameraSettings.PixelSize)" },
                            ["newValue"] = new MCPToolParameter { Type = "string", Description = "New value for the setting" }
                        },
                        Required = new List<string> { "settingpath", "newValue" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_switch_profile",
                    Description = "Switch to a different NINA profile by its profile ID",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["profileid"] = new MCPToolParameter { Type = "string", Description = "Profile ID to switch to (get IDs from nina_show_profile)" }
                        },
                        Required = new List<string> { "profileid" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_get_horizon",
                    Description = "Get the custom horizon data for the active profile",
                    InputSchema = new MCPToolInputSchema()
                },

                // Extended Sequence Operations
                new MCPTool
                {
                    Name = "nina_sequence_state",
                    Description = "Get the current state of the running sequence (idle, running, etc.)",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_sequence_edit",
                    Description = "Edit a value in the sequence by JSON path",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["path"] = new MCPToolParameter { Type = "string", Description = "JSON path to the value to edit" },
                            ["value"] = new MCPToolParameter { Type = "string", Description = "New value" }
                        },
                        Required = new List<string> { "path", "value" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_sequence_skip",
                    Description = "Skip items in the current sequence",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["type"] = new MCPToolParameter
                            {
                                Type = "string",
                                Description = "Type of skip operation",
                                Enum = new List<string> { "CurrentItems", "ToEnd", "ToImaging" }
                            }
                        },
                        Required = new List<string> { "type" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_sequence_reset",
                    Description = "Reset the sequence to its initial state",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_sequence_list_available",
                    Description = "List all available sequences that can be loaded",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_sequence_set_target",
                    Description = "Set or update a target in the sequence",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["name"] = new MCPToolParameter { Type = "string", Description = "Target name" },
                            ["ra"] = new MCPToolParameter { Type = "number", Description = "Right Ascension in degrees" },
                            ["dec"] = new MCPToolParameter { Type = "number", Description = "Declination in degrees" },
                            ["rotation"] = new MCPToolParameter { Type = "number", Description = "Rotation angle in degrees" },
                            ["index"] = new MCPToolParameter { Type = "integer", Description = "Target index in the sequence" }
                        },
                        Required = new List<string> { "name", "ra", "dec", "rotation", "index" }
                    }
                },

                // Event History
                new MCPTool
                {
                    Name = "nina_get_event_history",
                    Description = "Get the history of events (equipment connections, captures, errors, etc.)",
                    InputSchema = new MCPToolInputSchema()
                },

                // Image Operations
                new MCPTool
                {
                    Name = "nina_get_image",
                    Description = "Get a captured image by its index from the image history",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["index"] = new MCPToolParameter { Type = "integer", Description = "Image index in the history" },
                            ["resize"] = new MCPToolParameter { Type = "number", Description = "Resize factor (e.g., 0.5 for half size)" },
                            ["quality"] = new MCPToolParameter { Type = "integer", Description = "JPEG quality (0-100)" }
                        },
                        Required = new List<string> { "index" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_get_thumbnail",
                    Description = "Get a thumbnail of a captured image by its index",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["index"] = new MCPToolParameter { Type = "integer", Description = "Image index in the history" }
                        },
                        Required = new List<string> { "index" }
                    }
                },

                // Utility Operations
                new MCPTool
                {
                    Name = "nina_wait",
                    Description = "Wait for a specified duration in seconds",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["seconds"] = new MCPToolParameter { Type = "number", Description = "Duration to wait in seconds" }
                        },
                        Required = new List<string> { "seconds" }
                    }
                }
            };
        }

        /// <summary>
        /// Invoke a NINA Advanced API tool
        /// </summary>
        public async Task<MCPToolResult> InvokeToolAsync(string toolName, Dictionary<string, object>? arguments, CancellationToken cancellationToken = default)
        {
            if (_config == null)
            {
                return new MCPToolResult { Success = false, Error = "NINA Advanced API not configured" };
            }

            try
            {
                var baseUrl = $"http://{_config.NinaHost}:{_config.NinaPort}/v2/api";
                if (toolName == "nina_connect_all_equipment")
                {
                    Logger.Info("Invoking NINA API: Connect all equipment");
                    string[] endpoints = new[]
                    {
                        "equipment/camera/connect",
                        "equipment/mount/connect",
                        "equipment/focuser/connect",
                        "equipment/filterwheel/connect",
                        "equipment/guider/connect",
                        "equipment/dome/connect",
                        "equipment/rotator/connect",
                        "equipment/flatdevice/connect",
                        "equipment/switch/connect",
                        "equipment/weather/connect",
                        "equipment/safetymonitor/connect"
                    };

                    var tasks = endpoints.Select(async ep =>
                    {
                        try
                        {
                            var r = await _httpClient.GetAsync($"{baseUrl}/{ep}", cancellationToken);
                            var content = await r.Content.ReadAsStringAsync(cancellationToken);
                            return new { Endpoint = ep, Success = r.IsSuccessStatusCode, Content = content };
                        }
                        catch (Exception e)
                        {
                            return new { Endpoint = ep, Success = false, Content = e.Message };
                        }
                    });

                    var results = await Task.WhenAll(tasks);
                    
                    var data = new Dictionary<string, object>();
                    bool allSuccess = true;
                    var sb = new StringBuilder();
                    
                    foreach (var res in results)
                    {
                        data[res.Endpoint] = new { Success = res.Success, Content = res.Content };
                        if (!res.Success) allSuccess = false;
                        sb.AppendLine($"[{res.Endpoint}] Success: {res.Success}");
                    }

                    return new MCPToolResult
                    {
                        Success = allSuccess,
                        Content = $"Bulk connection attempt completed.\n{sb}",
                        Data = data
                    };
                }

                if (toolName == "nina_disconnect_all_equipment")
                {
                    Logger.Info("Invoking NINA API: Disconnect all equipment");
                    string[] endpoints = new[]
                    {
                        "equipment/camera/disconnect",
                        "equipment/mount/disconnect",
                        "equipment/focuser/disconnect",
                        "equipment/filterwheel/disconnect",
                        "equipment/guider/disconnect",
                        "equipment/dome/disconnect",
                        "equipment/rotator/disconnect",
                        "equipment/flatdevice/disconnect",
                        "equipment/switch/disconnect",
                        "equipment/weather/disconnect",
                        "equipment/safetymonitor/disconnect"
                    };

                    var tasks = endpoints.Select(async ep =>
                    {
                        try
                        {
                            var r = await _httpClient.GetAsync($"{baseUrl}/{ep}", cancellationToken);
                            var content = await r.Content.ReadAsStringAsync(cancellationToken);
                            return new { Endpoint = ep, Success = r.IsSuccessStatusCode, Content = content };
                        }
                        catch (Exception e)
                        {
                            return new { Endpoint = ep, Success = false, Content = e.Message };
                        }
                    });

                    var results = await Task.WhenAll(tasks);
                    
                    var data = new Dictionary<string, object>();
                    bool allSuccess = true;
                    var sb = new StringBuilder();
                    
                    foreach (var res in results)
                    {
                        data[res.Endpoint] = new { Success = res.Success, Content = res.Content };
                        if (!res.Success) allSuccess = false;
                        sb.AppendLine($"[{res.Endpoint}] Success: {res.Success}");
                    }

                    return new MCPToolResult
                    {
                        Success = allSuccess,
                        Content = $"Bulk disconnect attempt completed.\n{sb}",
                        Data = data
                    };
                }

                // Special handling for nina_wait - this is a delay, not an API call
                if (toolName == "nina_wait")
                {
                    var waitArgs = arguments ?? new Dictionary<string, object>();
                    if (waitArgs.TryGetValue("seconds", out var seconds))
                    {
                        var waitDuration = Convert.ToDouble(seconds);
                        Logger.Info($"Waiting for {waitDuration} seconds");
                        await Task.Delay(TimeSpan.FromSeconds(waitDuration), cancellationToken);
                        return new MCPToolResult
                        {
                            Success = true,
                            Content = $"Waited for {waitDuration} seconds",
                            Data = new Dictionary<string, object> { ["waited_seconds"] = waitDuration }
                        };
                    }
                    return new MCPToolResult { Success = false, Error = "Missing required parameter: seconds" };
                }

                string endpoint = MapToolToEndpoint(toolName, arguments);
                
                if (string.IsNullOrEmpty(endpoint))
                {
                    return new MCPToolResult { Success = false, Error = $"Unknown tool: {toolName}" };
                }

                Logger.Info($"Invoking NINA API: {endpoint}");
                
                var response = await _httpClient.GetAsync($"{baseUrl}/{endpoint}", cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = JObject.Parse(responseContent);
                    return new MCPToolResult
                    {
                        Success = result["Success"]?.Value<bool>() ?? true,
                        Content = responseContent,
                        Data = result.ToObject<Dictionary<string, object>>()
                    };
                }
                else
                {
                    return new MCPToolResult { Success = false, Error = $"API error: {response.StatusCode} - {responseContent}" };
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error invoking NINA tool {toolName}: {ex.Message}");
                return new MCPToolResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Map tool name and arguments to NINA API endpoint
        /// </summary>
        private string MapToolToEndpoint(string toolName, Dictionary<string, object>? args)
        {
            var argsDict = args ?? new Dictionary<string, object>();
            
            return toolName switch
            {
                // Status & Version
                "nina_get_status" => "equipment/info",
                "nina_get_version" => "version",
                "nina_get_nina_version" => BuildNinaVersionEndpoint(argsDict),
                "nina_get_start_time" => "application-start",
                
                // Camera
                "nina_connect_camera" => BuildEndpoint("equipment/camera/connect", argsDict, "device_id", "to"),
                "nina_disconnect_camera" => "equipment/camera/disconnect",
                "nina_get_camera_info" => "equipment/camera/info",
                "nina_list_camera_devices" => "equipment/camera/list-devices",
                "nina_rescan_camera" => "equipment/camera/rescan",
                "nina_set_camera_readout" => BuildEndpoint("equipment/camera/set-readout", argsDict, "mode", "mode"),
                "nina_set_camera_readout_image" => BuildEndpoint("equipment/camera/set-readout/image", argsDict, "mode", "mode"),
                "nina_set_camera_readout_snapshot" => BuildEndpoint("equipment/camera/set-readout/snapshot", argsDict, "mode", "mode"),
                "nina_set_camera_usb_limit" => BuildEndpoint("equipment/camera/usb-limit", argsDict, "limit", "limit"),
                "nina_capture_image" => BuildCaptureEndpoint(argsDict),
                "nina_start_cooling" => BuildCoolingEndpoint(argsDict),
                "nina_stop_cooling" => "equipment/camera/cool?cancel=true",
                "nina_abort_exposure" => "equipment/camera/abort-exposure",
                "nina_get_capture_statistics" => "equipment/camera/capture/statistics",
                
                // Mount
                "nina_connect_mount" => BuildEndpoint("equipment/mount/connect", argsDict, "device_id", "to"),
                "nina_disconnect_mount" => "equipment/mount/disconnect",
                "nina_get_mount_info" => "equipment/mount/info",
                "nina_list_mount_devices" => "equipment/mount/list-devices",
                "nina_rescan_mount" => "equipment/mount/rescan",
                "nina_slew_mount" => BuildSlewEndpoint(argsDict),
                "nina_park_mount" => "equipment/mount/park",
                "nina_unpark_mount" => "equipment/mount/unpark",
                "nina_set_mount_park_position" => "equipment/mount/set-park-position",
                "nina_home_mount" => "equipment/mount/home",
                "nina_stop_slew" => "equipment/mount/slew/stop",
                "nina_flip_mount" => "equipment/mount/flip",
                "nina_sync_mount" => BuildSyncMountEndpoint(argsDict),
                "nina_set_tracking_mode" => BuildTrackingEndpoint(argsDict),
                
                // Focuser
                "nina_connect_focuser" => BuildEndpoint("equipment/focuser/connect", argsDict, "device_id", "to"),
                "nina_disconnect_focuser" => "equipment/focuser/disconnect",
                "nina_get_focuser_info" => "equipment/focuser/info",
                "nina_list_focuser_devices" => "equipment/focuser/list-devices",
                "nina_rescan_focuser" => "equipment/focuser/rescan",
                "nina_move_focuser" => BuildFocuserMoveEndpoint(argsDict),
                "nina_stop_focuser_move" => "equipment/focuser/stop-move",
                
                // Filter Wheel
                "nina_connect_filterwheel" => BuildEndpoint("equipment/filterwheel/connect", argsDict, "device_id", "to"),
                "nina_disconnect_filterwheel" => "equipment/filterwheel/disconnect",
                "nina_get_filterwheel_info" => "equipment/filterwheel/info",
                "nina_list_filterwheel_devices" => "equipment/filterwheel/list-devices",
                "nina_rescan_filterwheel" => "equipment/filterwheel/rescan",
                "nina_change_filter" => BuildEndpoint("equipment/filterwheel/change-filter", argsDict, "filter_id", "filterId"),
                "nina_add_filter" => BuildAddFilterEndpoint(argsDict),
                "nina_remove_filter" => BuildEndpoint("equipment/filterwheel/remove-filter", argsDict, "filter_id", "filterId"),
                "nina_get_filter_info" => BuildEndpoint("equipment/filterwheel/filter-info", argsDict, "filter_id", "filterId"),
                
                // Guider
                "nina_connect_guider" => BuildEndpoint("equipment/guider/connect", argsDict, "device_id", "to"),
                "nina_disconnect_guider" => "equipment/guider/disconnect",
                "nina_get_guider_info" => "equipment/guider/info",
                "nina_list_guider_devices" => "equipment/guider/list-devices",
                "nina_rescan_guider" => "equipment/guider/rescan",
                "nina_start_guiding" => argsDict.ContainsKey("calibrate") && Convert.ToBoolean(argsDict["calibrate"]) ? "equipment/guider/start?calibrate=true" : "equipment/guider/start",
                "nina_stop_guiding" => "equipment/guider/stop",
                "nina_clear_guider_calibration" => "equipment/guider/clear-calibration",
                "nina_get_guider_graph" => "equipment/guider/graph",
                
                // Dome
                "nina_connect_dome" => BuildEndpoint("equipment/dome/connect", argsDict, "device_id", "to"),
                "nina_disconnect_dome" => "equipment/dome/disconnect",
                "nina_get_dome_info" => "equipment/dome/info",
                "nina_list_dome_devices" => "equipment/dome/list-devices",
                "nina_rescan_dome" => "equipment/dome/rescan",
                "nina_open_dome_shutter" => "equipment/dome/open", // Correct path based on API spec
                "nina_close_dome_shutter" => "equipment/dome/close", // Correct path based on API spec
                "nina_stop_dome" => "equipment/dome/stop",
                "nina_set_dome_follow" => BuildEndpoint("equipment/dome/set-follow", argsDict, "follow", "enabled"),
                "nina_sync_dome" => "equipment/dome/sync",
                "nina_park_dome" => "equipment/dome/park",
                "nina_set_dome_park_position" => "equipment/dome/set-park-position",
                "nina_home_dome" => "equipment/dome/home",
                "nina_slew_dome" => BuildDomeSlewEndpoint(argsDict),
                
                // Image History
                "nina_get_image_history" => BuildImageHistoryEndpoint(argsDict),
                
                // Application
                "nina_switch_tab" => BuildEndpoint("application/switch-tab", argsDict, "tab", "tab"),
                "nina_get_tab" => "application/get-tab",
                "nina_get_plugins" => "application/plugins",
                "nina_get_plugin_settings" => "plugin/settings",
                "nina_get_logs" => BuildLogsEndpoint(argsDict),
                
                // Flats
                "nina_skyflat" => BuildFlatsEndpoint("flats/skyflat", argsDict),
                "nina_auto_brightness_flat" => BuildFlatsEndpoint("flats/auto-brightness", argsDict),
                "nina_auto_exposure_flat" => BuildFlatsEndpoint("flats/auto-exposure", argsDict),
                "nina_trained_dark_flat" => BuildFlatsEndpoint("flats/trained-dark-flat", argsDict),
                "nina_trained_flat" => BuildFlatsEndpoint("flats/trained-flat", argsDict),
                "nina_stop_flats" => "flats/stop",
                "nina_get_flats_status" => "flats/status",
                
                // Rotator
                "nina_connect_rotator" => BuildEndpoint("equipment/rotator/connect", argsDict, "device_id", "to"),
                "nina_disconnect_rotator" => "equipment/rotator/disconnect",
                "nina_list_rotator_devices" => "equipment/rotator/list-devices",
                "nina_rescan_rotator" => "equipment/rotator/rescan",
                "nina_get_rotator_info" => "equipment/rotator/info",
                "nina_move_rotator" => BuildRotatorMoveEndpoint(argsDict),
                "nina_move_rotator_mechanical" => BuildEndpoint("equipment/rotator/move-mechanical", argsDict, "position", "position"),
                "nina_set_rotator_mechanical_range" => BuildRotatorRangeEndpoint(argsDict),
                "nina_halt_rotator" => "equipment/rotator/stop-move",
                "nina_set_rotator_reverse" => BuildEndpoint("equipment/rotator/reverse", argsDict, "enabled", "reverseDirection"),
                
                // Flat Panel
                "nina_connect_flatpanel" => BuildEndpoint("equipment/flatdevice/connect", argsDict, "device_id", "to"),
                "nina_disconnect_flatpanel" => "equipment/flatdevice/disconnect",
                "nina_list_flatpanel_devices" => "equipment/flatdevice/list-devices",
                "nina_rescan_flatpanel" => "equipment/flatdevice/rescan",
                "nina_get_flatpanel_info" => "equipment/flatdevice/info",
                "nina_set_flatpanel_light" => BuildEndpoint("equipment/flatdevice/set-light", argsDict, "on", "on"),
                "nina_set_flatpanel_cover" => BuildEndpoint("equipment/flatdevice/set-cover", argsDict, "closed", "closed"),
                "nina_set_flatpanel_brightness" => BuildEndpoint("equipment/flatdevice/set-brightness", argsDict, "brightness", "brightness"),
                
                // Switch
                "nina_connect_switch" => BuildEndpoint("equipment/switch/connect", argsDict, "device_id", "to"),
                "nina_disconnect_switch" => "equipment/switch/disconnect",
                "nina_list_switch_devices" => "equipment/switch/list-devices",
                "nina_rescan_switch" => "equipment/switch/rescan",
                "nina_get_switch_channels" => "equipment/switch/info",
                "nina_set_switch" => BuildSwitchEndpoint(argsDict),
                
                // Weather
                "nina_connect_weather" => BuildEndpoint("equipment/weather/connect", argsDict, "device_id", "to"),
                "nina_disconnect_weather" => "equipment/weather/disconnect",
                "nina_get_weather_info" => "equipment/weather/info",
                "nina_list_weather_sources" => "equipment/weather/list-devices",
                "nina_rescan_weather" => "equipment/weather/rescan",
                
                // Safety Monitor
                "nina_connect_safetymonitor" => BuildEndpoint("equipment/safetymonitor/connect", argsDict, "device_id", "to"),
                "nina_disconnect_safetymonitor" => "equipment/safetymonitor/disconnect",
                "nina_get_safetymonitor_info" => "equipment/safetymonitor/info",
                "nina_list_safetymonitor_devices" => "equipment/safetymonitor/list-devices",
                "nina_rescan_safetymonitor" => "equipment/safetymonitor/rescan",
                
                // Advanced Camera
                "nina_set_binning" => BuildEndpoint("equipment/camera/set-binning", argsDict, "binning", "binning"),
                "nina_control_dew_heater" => BuildEndpoint("equipment/camera/dew-heater", argsDict, "power", "power"),
                "nina_start_warming" => BuildWarmingEndpoint(argsDict),
                
                // Advanced Focuser
                "nina_start_autofocus" => "equipment/focuser/auto-focus",
                "nina_cancel_autofocus" => "equipment/focuser/auto-focus?cancel=true",
                "nina_get_last_autofocus" => "equipment/focuser/last-af",
                "nina_halt_focuser" => "equipment/focuser/stop-move",
                
                // Sequence
                "nina_sequence_start" => BuildSequenceStartEndpoint(argsDict),
                "nina_sequence_stop" => "sequence/stop",
                "nina_sequence_load" => BuildEndpoint("sequence/load", argsDict, "sequenceName", "sequenceName"),
                "nina_sequence_json" => "sequence/json",
                "nina_sequence_state" => "sequence/state",
                "nina_sequence_edit" => BuildSequenceEditEndpoint(argsDict),
                "nina_sequence_skip" => BuildEndpoint("sequence/skip", argsDict, "type", "type"),
                "nina_sequence_reset" => "sequence/reset",
                "nina_sequence_list_available" => "sequence/list-available",
                "nina_sequence_set_target" => BuildSequenceSetTargetEndpoint(argsDict),
                
                // Profile
                "nina_show_profile" => BuildShowProfileEndpoint(argsDict),
                "nina_change_profile_value" => BuildChangeProfileEndpoint(argsDict),
                "nina_switch_profile" => BuildEndpoint("profile/switch", argsDict, "profileid", "profileid"),
                "nina_get_horizon" => "profile/horizon",
                
                // Event History
                "nina_get_event_history" => "event-history",
                
                // Image
                "nina_get_image" => BuildGetImageEndpoint(argsDict),
                "nina_get_thumbnail" => BuildGetThumbnailEndpoint(argsDict),
                
                // Plate Solving
                "nina_platesolve_capsolve" => BuildPlateSolveEndpoint("plate-solve/capsolve", argsDict),
                "nina_platesolve_sync" => BuildPlateSolveEndpoint("plate-solve/sync", argsDict),
                "nina_platesolve_center" => BuildPlateSolveCenterEndpoint(argsDict),
                
                // Framing Assistant
                "nina_get_framingassistant_info" => "framing/info",
                "nina_set_framingassistant_source" => BuildEndpoint("framing/set-source", argsDict, "source", "source"),
                "nina_set_framingassistant_coordinates" => BuildFramingCoordinatesEndpoint(argsDict),
                "nina_set_framingassistant_rotation" => BuildEndpoint("framing/set-rotation", argsDict, "rotation", "rotation"),
                "nina_determine_framingassistant_rotation" => BuildDetermineRotationEndpoint(argsDict),
                "nina_framingassistant_slew" => BuildFramingSlewEndpoint(argsDict),
                "nina_moon_separation" => BuildMoonSeparationEndpoint(argsDict),
                
                // Utility
                "nina_wait" => "",  // Special handling needed - this is a delay, not an API call
                
                _ => ""
            };
        }

        private string BuildAddFilterEndpoint(Dictionary<string, object> args)
        {
            return "equipment/filterwheel/add-filter";
        }

        private string BuildRotatorRangeEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("range", out var range))
                parameters.Add($"range={Uri.EscapeDataString(range.ToString() ?? "")}");
            if (args.TryGetValue("rangeStartPosition", out var startPos))
                parameters.Add($"rangeStartPosition={startPos}");
                
            return parameters.Count > 0 ? $"equipment/rotator/set-mechanical-range?{string.Join("&", parameters)}" : "equipment/rotator/set-mechanical-range";
        }

        private string BuildEndpoint(string baseEndpoint, Dictionary<string, object> args, string argKey, string paramName)
        {
            if (args.TryGetValue(argKey, out var value) && value != null)
            {
                return $"{baseEndpoint}?{paramName}={Uri.EscapeDataString(value.ToString() ?? "")}";
            }
            return baseEndpoint;
        }

        private string BuildCaptureEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string> { "save=true" };
            
            if (args.TryGetValue("duration", out var duration))
                parameters.Add($"duration={duration}");
            if (args.TryGetValue("gain", out var gain))
                parameters.Add($"gain={gain}");

            bool wantsSolve = args.TryGetValue("solve", out var solve) && Convert.ToBoolean(solve);
            bool wantsDownload = args.TryGetValue("download", out var download) && Convert.ToBoolean(download);

            if (wantsDownload)
            {
                parameters.Add("stream=true");
                parameters.Add("waitForResult=true");
            }
            else if (wantsSolve)
            {
                // When plate solving, we must wait for the result even without downloading the image
                parameters.Add("omitImage=true");
                parameters.Add("waitForResult=true");
            }
            else
            {
                parameters.Add("omitImage=true");
                parameters.Add("waitForResult=false");
            }

            if (wantsSolve)
                parameters.Add("solve=true");
                
            return $"equipment/camera/capture?{string.Join("&", parameters)}";
        }

        private string BuildCoolingEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string> { "cancel=false" };
            
            if (args.TryGetValue("temperature", out var temp))
                parameters.Add($"temperature={temp}");
            if (args.TryGetValue("duration", out var dur))
                parameters.Add($"minutes={dur}");
                
            return $"equipment/camera/cool?{string.Join("&", parameters)}";
        }

        private string BuildSlewEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("ra", out var ra))
            {
                // Convert RA from hours to degrees (1 hour = 15 degrees)
                var raHours = Convert.ToDouble(ra);
                var raDegrees = raHours * 15.0;
                parameters.Add($"ra={raDegrees}");
            }
            if (args.TryGetValue("dec", out var dec))
                parameters.Add($"dec={dec}");
            
            // Default waitForResult to true
            var waitForResult = true;
            if (args.TryGetValue("waitForResult", out var wait))
                waitForResult = Convert.ToBoolean(wait);
            else if (args.TryGetValue("wait_for_completion", out var waitLegacy))
                waitForResult = Convert.ToBoolean(waitLegacy);
            parameters.Add($"waitForResult={waitForResult.ToString().ToLower()}");
            
            if (args.TryGetValue("center", out var center) && Convert.ToBoolean(center))
                parameters.Add("center=true");
            if (args.TryGetValue("rotate", out var rotate) && Convert.ToBoolean(rotate))
                parameters.Add("rotate=true");
            if (args.TryGetValue("rotationAngle", out var rotAngle))
                parameters.Add($"rotationAngle={rotAngle}");
            
            return $"equipment/mount/slew?{string.Join("&", parameters)}";
        }

        private string BuildSyncMountEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("ra", out var ra))
            {
                // Convert RA from hours to degrees (1 hour = 15 degrees)
                var raHours = Convert.ToDouble(ra);
                var raDegrees = raHours * 15.0;
                parameters.Add($"ra={raDegrees}");
            }
            if (args.TryGetValue("dec", out var dec))
                parameters.Add($"dec={dec}");
                
            return parameters.Count > 0 ? $"equipment/mount/sync?{string.Join("&", parameters)}" : "equipment/mount/sync";
        }

        private string BuildNinaVersionEndpoint(Dictionary<string, object> args)
        {
            if (args.TryGetValue("friendly", out var friendly) && Convert.ToBoolean(friendly))
                return "version/nina?friendly=true";
            return "version/nina";
        }

        private string BuildLogsEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("lineCount", out var lineCount))
                parameters.Add($"lineCount={lineCount}");
            if (args.TryGetValue("level", out var level) && level != null)
                parameters.Add($"level={Uri.EscapeDataString(level.ToString() ?? "")}");
                
            return $"application/logs?{string.Join("&", parameters)}";
        }

        private string BuildTrackingEndpoint(Dictionary<string, object> args)
        {
            if (args.TryGetValue("mode", out var mode))
            {
                var modeValue = mode?.ToString()?.ToUpper() switch
                {
                    "SIDEREAL" => "0",
                    "LUNAR" => "1",
                    "SOLAR" => "2",
                    "KING" => "3",
                    "STOPPED" => "4",
                    _ => "0"
                };
                return $"equipment/mount/tracking?mode={modeValue}";
            }
            return "equipment/mount/tracking?mode=0";
        }

        private string BuildFocuserMoveEndpoint(Dictionary<string, object> args)
        {
            if (args.TryGetValue("position", out var pos))
                return $"equipment/focuser/move?position={pos}";
            return "equipment/focuser/move";
        }

        private string BuildImageHistoryEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("all", out var all))
                parameters.Add($"all={all.ToString()?.ToLower()}");
            else
                parameters.Add("all=true");
            if (args.TryGetValue("index", out var index))
                parameters.Add($"index={index}");
            if (args.TryGetValue("count", out var count))
                parameters.Add($"count={count}");
            if (args.TryGetValue("imageType", out var imageType))
                parameters.Add($"imageType={Uri.EscapeDataString(imageType.ToString() ?? "")}");
                
            return $"image-history?{string.Join("&", parameters)}";
        }

        private string BuildFlatsEndpoint(string baseEndpoint, Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            string[] flatParams = { "count", "minExposure", "maxExposure", "histogramMean", "meanTolerance",
                                    "dither", "filterId", "binning", "gain", "offset", "keepClosed",
                                    "exposureTime", "minBrightness", "maxBrightness", "brightness" };
            foreach (var param in flatParams)
            {
                if (args.TryGetValue(param, out var value))
                    parameters.Add($"{param}={Uri.EscapeDataString(value.ToString() ?? "")}");
            }
                
            return parameters.Count > 0 ? $"{baseEndpoint}?{string.Join("&", parameters)}" : baseEndpoint;
        }

        private string BuildRotatorMoveEndpoint(Dictionary<string, object> args)
        {
            if (args.TryGetValue("position", out var pos))
                return $"equipment/rotator/move?position={pos}";
            return "equipment/rotator/move";
        }

        private string BuildSwitchEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("index", out var index))
                parameters.Add($"index={index}");
            if (args.TryGetValue("value", out var value))
                parameters.Add($"value={value}");
                
            return $"equipment/switch/set?{string.Join("&", parameters)}";
        }

        private string BuildWarmingEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string> { "cancel=false" };
            
            if (args.TryGetValue("duration", out var dur))
                parameters.Add($"minutes={dur}");
                
            return $"equipment/camera/warm?{string.Join("&", parameters)}";
        }

        private string BuildAutofocusEndpoint(Dictionary<string, object> args)
        {
            return "equipment/focuser/auto-focus";
        }

        private string BuildSequenceStartEndpoint(Dictionary<string, object> args)
        {
            if (args.TryGetValue("skipValidation", out var skip) && Convert.ToBoolean(skip))
                return "sequence/start?skipValidation=true";
            return "sequence/start";
        }

        private string BuildPlateSolveEndpoint(string baseEndpoint, Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("blind", out var blind))
                parameters.Add($"blind={blind.ToString()?.ToLower()}");
                
            return parameters.Count > 0 ? $"{baseEndpoint}?{string.Join("&", parameters)}" : baseEndpoint;
        }

        private string BuildFramingCoordinatesEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("ra", out var ra))
            {
                // API expects RAangle in degrees, input is in hours (0-24)
                var raHours = Convert.ToDouble(ra);
                var raDegrees = raHours * 15.0;
                parameters.Add($"RAangle={raDegrees}");
            }
            if (args.TryGetValue("dec", out var dec))
                parameters.Add($"DecAngle={dec}");
                
            return $"framing/set-coordinates?{string.Join("&", parameters)}";
        }

        private string BuildFramingSlewEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("slew_option", out var slewOption) && slewOption != null)
                parameters.Add($"slew_option={Uri.EscapeDataString(slewOption.ToString() ?? "")}");
            if (args.TryGetValue("waitForResult", out var waitForResult))
                parameters.Add($"waitForResult={waitForResult.ToString()?.ToLower()}");
                
            return parameters.Count > 0 ? $"framing/slew?{string.Join("&", parameters)}" : "framing/slew";
        }

        private string BuildDetermineRotationEndpoint(Dictionary<string, object> args)
        {
            if (args.TryGetValue("waitForResult", out var waitForResult))
                return $"framing/determine-rotation?waitForResult={waitForResult.ToString()?.ToLower()}";
            return "framing/determine-rotation";
        }

        private string BuildMoonSeparationEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("ra", out var ra))
            {
                // API expects RA in degrees, input is in hours (0-24)
                var raHours = Convert.ToDouble(ra);
                var raDegrees = raHours * 15.0;
                parameters.Add($"ra={raDegrees}");
            }
            if (args.TryGetValue("dec", out var dec))
                parameters.Add($"dec={dec}");
                
            return $"astro-util/moon-separation?{string.Join("&", parameters)}";
        }

        private string BuildPlateSolveCenterEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("ra", out var ra))
            {
                // Convert RA from hours to degrees (1 hour = 15 degrees), consistent with BuildSlewEndpoint
                var raHours = Convert.ToDouble(ra);
                var raDegrees = raHours * 15.0;
                parameters.Add($"ra={raDegrees}");
            }
            if (args.TryGetValue("dec", out var dec))
                parameters.Add($"dec={dec}");
                
            return $"plate-solve/center?{string.Join("&", parameters)}";
        }

        private string BuildDomeSlewEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("azimuth", out var azimuth))
                parameters.Add($"azimuth={azimuth}");
            if (args.TryGetValue("waitToFinish", out var waitToFinish))
                parameters.Add($"waitToFinish={waitToFinish.ToString()?.ToLower()}");
                
            return parameters.Count > 0 ? $"equipment/dome/slew?{string.Join("&", parameters)}" : "equipment/dome/slew";
        }

        private string BuildShowProfileEndpoint(Dictionary<string, object> args)
        {
            if (args.TryGetValue("active", out var active))
                return $"profile/show?active={active.ToString()?.ToLower()}";
            return "profile/show";
        }

        private string BuildChangeProfileEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("settingpath", out var settingpath))
                parameters.Add($"settingpath={Uri.EscapeDataString(settingpath.ToString() ?? "")}");
            if (args.TryGetValue("newValue", out var newValue))
                parameters.Add($"newValue={Uri.EscapeDataString(newValue.ToString() ?? "")}");
                
            return $"profile/change-value?{string.Join("&", parameters)}";
        }

        private string BuildSequenceEditEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("path", out var path))
                parameters.Add($"path={Uri.EscapeDataString(path.ToString() ?? "")}");
            if (args.TryGetValue("value", out var value))
                parameters.Add($"value={Uri.EscapeDataString(value.ToString() ?? "")}");
                
            return $"sequence/edit?{string.Join("&", parameters)}";
        }

        private string BuildSequenceSetTargetEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("name", out var name))
                parameters.Add($"name={Uri.EscapeDataString(name.ToString() ?? "")}");
            if (args.TryGetValue("ra", out var ra))
                parameters.Add($"ra={ra}");
            if (args.TryGetValue("dec", out var dec))
                parameters.Add($"dec={dec}");
            if (args.TryGetValue("rotation", out var rotation))
                parameters.Add($"rotation={rotation}");
            if (args.TryGetValue("index", out var index))
                parameters.Add($"index={index}");
                
            return $"sequence/set-target?{string.Join("&", parameters)}";
        }

        private string BuildGetImageEndpoint(Dictionary<string, object> args)
        {
            var index = args.TryGetValue("index", out var idx) ? idx.ToString() : "0";
            var parameters = new List<string>();
            
            if (args.TryGetValue("resize", out var resize))
                parameters.Add($"resize={resize}");
            if (args.TryGetValue("quality", out var quality))
                parameters.Add($"quality={quality}");
                
            return parameters.Count > 0 ? $"image/{index}?{string.Join("&", parameters)}" : $"image/{index}";
        }

        private string BuildGetThumbnailEndpoint(Dictionary<string, object> args)
        {
            var index = args.TryGetValue("index", out var idx) ? idx.ToString() : "0";
            return $"image/thumbnail/{index}";
        }

        /// <summary>
        /// Close the client
        /// </summary>
        public void Close()
        {
            _httpClient?.Dispose();
            _isConnected = false;
        }
    }
}
