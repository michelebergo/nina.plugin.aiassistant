using System;
using System.Collections.Generic;
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
            _httpClient.Timeout = TimeSpan.FromSeconds(120); // Long timeout for captures
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
                    Description = "Get the version of NINA and the Advanced API",
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
                            ["solve"] = new MCPToolParameter { Type = "boolean", Description = "Whether to plate solve the image" }
                        }
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
                    Name = "nina_slew_mount",
                    Description = "Slew the mount to specified coordinates",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["ra"] = new MCPToolParameter { Type = "number", Description = "Right Ascension in hours (0-24)" },
                            ["dec"] = new MCPToolParameter { Type = "number", Description = "Declination in degrees (-90 to +90)" },
                            ["wait_for_completion"] = new MCPToolParameter { Type = "boolean", Description = "Whether to wait for slew to complete", Default = "true" }
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
                    Name = "nina_home_mount",
                    Description = "Send the mount to its home position",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_stop_slew",
                    Description = "Stop the mount's current slew",
                    InputSchema = new MCPToolInputSchema()
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
                    Name = "nina_move_focuser",
                    Description = "Move the focuser to a specific position",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["position"] = new MCPToolParameter { Type = "integer", Description = "Target position in steps" },
                            ["relative"] = new MCPToolParameter { Type = "boolean", Description = "Whether position is relative to current" }
                        },
                        Required = new List<string> { "position" }
                    }
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
                    Name = "nina_calibrate_guider",
                    Description = "Calibrate the guider without starting guiding",
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
                            ["azimuth"] = new MCPToolParameter { Type = "number", Description = "Target azimuth in degrees" }
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
                            ["limit"] = new MCPToolParameter { Type = "integer", Description = "Maximum number of images to return" },
                            ["offset"] = new MCPToolParameter { Type = "integer", Description = "Offset for pagination" }
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
                            ["tab"] = new MCPToolParameter { Type = "string", Description = "Name of the tab to switch to" }
                        },
                        Required = new List<string> { "tab" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_get_plugins",
                    Description = "Get information about installed NINA plugins",
                    InputSchema = new MCPToolInputSchema()
                },

                // Flats
                new MCPTool
                {
                    Name = "nina_start_flats",
                    Description = "Start capturing flat frames",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["count"] = new MCPToolParameter { Type = "integer", Description = "Number of flats to capture" },
                            ["binning"] = new MCPToolParameter { Type = "string", Description = "Binning mode (e.g., 1x1, 2x2)" },
                            ["gain"] = new MCPToolParameter { Type = "integer", Description = "Camera gain" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_stop_flats",
                    Description = "Stop capturing flat frames",
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
                    Name = "nina_get_rotator_info",
                    Description = "Get information about the connected rotator",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_move_rotator",
                    Description = "Move the rotator to a specific position",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["position"] = new MCPToolParameter { Type = "number", Description = "Target position in degrees" },
                            ["relative"] = new MCPToolParameter { Type = "boolean", Description = "Whether position is relative to current" }
                        },
                        Required = new List<string> { "position" }
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
                    Name = "nina_sync_rotator",
                    Description = "Sync the rotator to a specific position",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["position"] = new MCPToolParameter { Type = "number", Description = "Position to sync to in degrees" }
                        },
                        Required = new List<string> { "position" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_set_rotator_reverse",
                    Description = "Set the rotator's reverse state",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["enabled"] = new MCPToolParameter { Type = "boolean", Description = "True to enable reverse, False to disable" }
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
                    Name = "nina_get_flatpanel_info",
                    Description = "Get information about the connected flat panel",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_set_flatpanel_light",
                    Description = "Set the flat panel light state",
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
                    Name = "nina_set_camera_gain",
                    Description = "Set the camera gain",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["gain"] = new MCPToolParameter { Type = "integer", Description = "Gain value" }
                        },
                        Required = new List<string> { "gain" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_set_camera_offset",
                    Description = "Set the camera offset",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["offset"] = new MCPToolParameter { Type = "integer", Description = "Offset value" }
                        },
                        Required = new List<string> { "offset" }
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
                    Description = "Start an autofocus routine",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["method"] = new MCPToolParameter { Type = "string", Description = "Autofocus method" }
                        }
                    }
                },
                new MCPTool
                {
                    Name = "nina_cancel_autofocus",
                    Description = "Cancel the current autofocus routine",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_get_autofocus_status",
                    Description = "Get the status of the current autofocus routine",
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
                            ["ra"] = new MCPToolParameter { Type = "number", Description = "Right Ascension in degrees" },
                            ["dec"] = new MCPToolParameter { Type = "number", Description = "Declination in degrees" }
                        },
                        Required = new List<string> { "ra", "dec" }
                    }
                },

                // Framing Assistant Operations
                new MCPTool
                {
                    Name = "nina_get_framingassistant_info",
                    Description = "Get information about the current framing assistant state",
                    InputSchema = new MCPToolInputSchema()
                },
                new MCPTool
                {
                    Name = "nina_set_framingassistant_source",
                    Description = "Set the source/target for the framing assistant",
                    InputSchema = new MCPToolInputSchema
                    {
                        Properties = new Dictionary<string, MCPToolParameter>
                        {
                            ["source"] = new MCPToolParameter { Type = "string", Description = "Source identifier or name (e.g., M31, NGC7000)" }
                        },
                        Required = new List<string> { "source" }
                    }
                },
                new MCPTool
                {
                    Name = "nina_framingassistant_slew",
                    Description = "Slew the mount to the framing assistant target coordinates",
                    InputSchema = new MCPToolInputSchema()
                },

                // Utility Operations
                new MCPTool
                {
                    Name = "nina_time_now",
                    Description = "Get the current time from the computer in various formats",
                    InputSchema = new MCPToolInputSchema()
                },
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
                "nina_get_status" => "equipment/camera/info",  // TODO: This needs special handling to query all equipment
                "nina_get_version" => "version",  // FIXED: Correct endpoint
                
                // Camera
                "nina_connect_camera" => BuildEndpoint("equipment/camera/connect", argsDict, "device_id", "to"),
                "nina_disconnect_camera" => "equipment/camera/disconnect",
                "nina_get_camera_info" => "equipment/camera/info",
                "nina_list_camera_devices" => "equipment/camera/list-devices",
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
                "nina_slew_mount" => BuildSlewEndpoint(argsDict),
                "nina_park_mount" => "equipment/mount/park",
                "nina_unpark_mount" => "equipment/mount/unpark",
                "nina_home_mount" => "equipment/mount/home",
                "nina_stop_slew" => "equipment/mount/stop-slew",
                "nina_flip_mount" => "equipment/mount/flip",
                "nina_set_tracking_mode" => BuildTrackingEndpoint(argsDict),
                
                // Focuser
                "nina_connect_focuser" => BuildEndpoint("equipment/focuser/connect", argsDict, "device_id", "to"),
                "nina_disconnect_focuser" => "equipment/focuser/disconnect",
                "nina_get_focuser_info" => "equipment/focuser/info",
                "nina_list_focuser_devices" => "equipment/focuser/list-devices",
                "nina_move_focuser" => BuildFocuserMoveEndpoint(argsDict),
                
                // Filter Wheel
                "nina_connect_filterwheel" => BuildEndpoint("equipment/filterwheel/connect", argsDict, "device_id", "to"),
                "nina_disconnect_filterwheel" => "equipment/filterwheel/disconnect",
                "nina_get_filterwheel_info" => "equipment/filterwheel/info",
                "nina_list_filterwheel_devices" => "equipment/filterwheel/list-devices",
                "nina_change_filter" => BuildEndpoint("equipment/filterwheel/change-filter", argsDict, "filter_id", "filterId"),
                
                // Guider
                "nina_connect_guider" => BuildEndpoint("equipment/guider/connect", argsDict, "device_id", "to"),
                "nina_disconnect_guider" => "equipment/guider/disconnect",
                "nina_get_guider_info" => "equipment/guider/info",
                "nina_list_guider_devices" => "equipment/guider/list-devices",
                "nina_start_guiding" => argsDict.ContainsKey("calibrate") && Convert.ToBoolean(argsDict["calibrate"]) ? "equipment/guider/start?calibrate=true" : "equipment/guider/start",
                "nina_stop_guiding" => "equipment/guider/stop",
                "nina_calibrate_guider" => "equipment/guider/calibrate",
                "nina_get_guider_graph" => "equipment/guider/graph",
                
                // Dome
                "nina_connect_dome" => BuildEndpoint("equipment/dome/connect", argsDict, "device_id", "to"),
                "nina_disconnect_dome" => "equipment/dome/disconnect",
                "nina_get_dome_info" => "equipment/dome/info",
                "nina_list_dome_devices" => "equipment/dome/list-devices",
                "nina_open_dome_shutter" => "equipment/dome/open-shutter",
                "nina_close_dome_shutter" => "equipment/dome/close-shutter",
                "nina_park_dome" => "equipment/dome/park",
                "nina_home_dome" => "equipment/dome/home",
                "nina_slew_dome" => BuildEndpoint("equipment/dome/slew", argsDict, "azimuth", "azimuth"),
                
                // Image History
                "nina_get_image_history" => BuildImageHistoryEndpoint(argsDict),
                
                // Application
                "nina_switch_tab" => BuildEndpoint("application/switch-tab", argsDict, "tab", "tab"),
                "nina_get_plugins" => "application/plugins",
                
                // Flats
                "nina_start_flats" => BuildFlatsEndpoint(argsDict),
                "nina_stop_flats" => "flats/stop",
                "nina_get_flats_status" => "flats/status",
                
                // Rotator
                "nina_connect_rotator" => BuildEndpoint("equipment/rotator/connect", argsDict, "device_id", "to"),
                "nina_disconnect_rotator" => "equipment/rotator/disconnect",
                "nina_list_rotator_devices" => "equipment/rotator/list-devices",
                "nina_get_rotator_info" => "equipment/rotator/info",
                "nina_move_rotator" => BuildRotatorMoveEndpoint(argsDict),
                "nina_halt_rotator" => "equipment/rotator/halt",
                "nina_sync_rotator" => BuildEndpoint("equipment/rotator/sync", argsDict, "position", "position"),
                "nina_set_rotator_reverse" => BuildEndpoint("equipment/rotator/reverse", argsDict, "enabled", "enabled"),
                
                // Flat Panel
                "nina_connect_flatpanel" => BuildEndpoint("equipment/flatdevice/connect", argsDict, "device_id", "to"),
                "nina_disconnect_flatpanel" => "equipment/flatdevice/disconnect",
                "nina_list_flatpanel_devices" => "equipment/flatdevice/list-devices",
                "nina_get_flatpanel_info" => "equipment/flatdevice/info",
                "nina_set_flatpanel_light" => BuildEndpoint("equipment/flatdevice/set-light", argsDict, "power", "power"),
                "nina_set_flatpanel_cover" => BuildEndpoint("equipment/flatdevice/set-cover", argsDict, "closed", "closed"),
                "nina_set_flatpanel_brightness" => BuildEndpoint("equipment/flatdevice/set-brightness", argsDict, "brightness", "brightness"),
                
                // Switch
                "nina_connect_switch" => BuildEndpoint("equipment/switch/connect", argsDict, "device_id", "to"),
                "nina_disconnect_switch" => "equipment/switch/disconnect",
                "nina_list_switch_devices" => "equipment/switch/list-devices",
                "nina_get_switch_channels" => "equipment/switch/info",
                "nina_set_switch" => BuildSwitchEndpoint(argsDict),
                
                // Weather
                "nina_connect_weather" => BuildEndpoint("equipment/weather/connect", argsDict, "device_id", "to"),
                "nina_disconnect_weather" => "equipment/weather/disconnect",
                "nina_get_weather_info" => "equipment/weather/info",
                "nina_list_weather_sources" => "equipment/weather/list-devices",
                
                // Safety Monitor
                "nina_connect_safetymonitor" => BuildEndpoint("equipment/safetymonitor/connect", argsDict, "device_id", "to"),
                "nina_disconnect_safetymonitor" => "equipment/safetymonitor/disconnect",
                "nina_get_safetymonitor_info" => "equipment/safetymonitor/info",
                "nina_list_safetymonitor_devices" => "equipment/safetymonitor/list-devices",
                
                // Advanced Camera
                "nina_set_binning" => BuildEndpoint("equipment/camera/set-binning", argsDict, "binning", "binning"),
                "nina_control_dew_heater" => BuildEndpoint("equipment/camera/dew-heater", argsDict, "power", "power"),
                "nina_set_camera_gain" => BuildEndpoint("equipment/camera/set-gain", argsDict, "gain", "gain"),
                "nina_set_camera_offset" => BuildEndpoint("equipment/camera/set-offset", argsDict, "offset", "offset"),
                "nina_start_warming" => BuildWarmingEndpoint(argsDict),
                
                // Advanced Focuser
                "nina_start_autofocus" => BuildAutofocusEndpoint(argsDict),
                "nina_cancel_autofocus" => "equipment/focuser/autofocus/cancel",
                "nina_get_autofocus_status" => "equipment/focuser/autofocus/status",
                "nina_halt_focuser" => "equipment/focuser/halt",
                
                // Sequence
                "nina_sequence_start" => BuildSequenceStartEndpoint(argsDict),
                "nina_sequence_stop" => "sequence/stop",
                "nina_sequence_load" => BuildEndpoint("sequence/load", argsDict, "sequenceName", "sequenceName"),
                "nina_sequence_json" => "sequence/json",
                
                // Plate Solving
                "nina_platesolve_capsolve" => BuildPlateSolveEndpoint("plate-solve/capsolve", argsDict),
                "nina_platesolve_sync" => BuildPlateSolveEndpoint("plate-solve/sync", argsDict),
                "nina_platesolve_center" => BuildPlateSolveCenterEndpoint(argsDict),
                
                // Framing Assistant
                "nina_get_framingassistant_info" => "framing/info",
                "nina_set_framingassistant_source" => BuildEndpoint("framing/set-source", argsDict, "source", "source"),
                "nina_framingassistant_slew" => "framing/slew",
                
                // Utility
                "nina_time_now" => "application/time",
                "nina_wait" => "",  // Special handling needed - this is a delay, not an API call
                
                _ => ""
            };
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
            if (args.TryGetValue("download", out var download) && Convert.ToBoolean(download))
            {
                parameters.Add("stream=true");
                parameters.Add("waitForResult=true");
            }
            else
            {
                parameters.Add("omitImage=true");
                parameters.Add("waitForResult=false");
            }
            if (args.TryGetValue("solve", out var solve) && Convert.ToBoolean(solve))
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
            
            var waitForCompletion = true;
            if (args.TryGetValue("wait_for_completion", out var wait))
                waitForCompletion = Convert.ToBoolean(wait);
            parameters.Add($"waitForResult={waitForCompletion.ToString().ToLower()}");
            
            return $"equipment/mount/slew?{string.Join("&", parameters)}";
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
            var parameters = new List<string>();
            
            if (args.TryGetValue("position", out var pos))
                parameters.Add($"position={pos}");
            if (args.TryGetValue("relative", out var rel) && Convert.ToBoolean(rel))
                parameters.Add("relative=true");
                
            return $"equipment/focuser/move?{string.Join("&", parameters)}";
        }

        private string BuildImageHistoryEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string> { "all=true" };
            
            if (args.TryGetValue("limit", out var limit))
                parameters.Add($"limit={limit}");
            if (args.TryGetValue("offset", out var offset))
                parameters.Add($"offset={offset}");
                
            return $"image-history?{string.Join("&", parameters)}";
        }

        private string BuildFlatsEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("count", out var count))
                parameters.Add($"count={count}");
            if (args.TryGetValue("binning", out var binning))
                parameters.Add($"binning={binning}");
            if (args.TryGetValue("gain", out var gain))
                parameters.Add($"gain={gain}");
                
            return parameters.Count > 0 ? $"flats/start?{string.Join("&", parameters)}" : "flats/start";
        }

        private string BuildRotatorMoveEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("position", out var pos))
                parameters.Add($"position={pos}");
            if (args.TryGetValue("relative", out var rel) && Convert.ToBoolean(rel))
                parameters.Add("relative=true");
                
            return $"equipment/rotator/move?{string.Join("&", parameters)}";
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
            var parameters = new List<string>();
            
            if (args.TryGetValue("method", out var method))
                parameters.Add($"method={method}");
                
            return parameters.Count > 0 ? $"equipment/focuser/autofocus?{string.Join("&", parameters)}" : "equipment/focuser/autofocus";
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

        private string BuildPlateSolveCenterEndpoint(Dictionary<string, object> args)
        {
            var parameters = new List<string>();
            
            if (args.TryGetValue("ra", out var ra))
                parameters.Add($"ra={ra}");
            if (args.TryGetValue("dec", out var dec))
                parameters.Add($"dec={dec}");
                
            return $"plate-solve/center?{string.Join("&", parameters)}";
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
