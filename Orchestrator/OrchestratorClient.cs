using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NINA.Plugin.AIAssistant.Orchestrator
{
    /// <summary>
    /// Plain DTOs mirroring the nina.autopilot dashboard's JSON shape.
    /// Source of truth: nina.autopilot/src/nina_autopilot/dashboard.py.
    /// </summary>
    public class OrchestratorStatus
    {
        [JsonProperty("phase")]
        public string Phase { get; set; } = "BOOT";

        [JsonProperty("session")]
        public OrchestratorSession? Session { get; set; }

        [JsonProperty("budget")]
        public OrchestratorBudget? Budget { get; set; }
    }

    public class OrchestratorSession
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("sequence_file")]
        public string? SequenceFile { get; set; }

        [JsonProperty("phase")]
        public string? Phase { get; set; }

        [JsonProperty("started_at")]
        public string? StartedAt { get; set; }

        [JsonProperty("ended_at")]
        public string? EndedAt { get; set; }

        [JsonProperty("end_reason")]
        public string? EndReason { get; set; }
    }

    public class OrchestratorBudget
    {
        [JsonProperty("budget_usd")]
        public double? BudgetUsd { get; set; }

        [JsonProperty("spent_usd")]
        public double? SpentUsd { get; set; }

        [JsonProperty("remaining_usd")]
        public double? RemainingUsd { get; set; }

        [JsonProperty("state")]
        public string State { get; set; } = "normal";
    }

    public class OrchestratorEvent
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("session_id")]
        public int SessionId { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; } = "";

        [JsonProperty("kind")]
        public string Kind { get; set; } = "";

        [JsonProperty("payload")]
        public JObject? Payload { get; set; }
    }

    /// <summary>
    /// Thin HTTP client over the nina.autopilot dashboard's REST API.
    /// All methods are async and cancellable. Errors are surfaced as
    /// exceptions — the ViewModel catches them and shows the user a
    /// connection-lost state rather than crashing the panel.
    /// </summary>
    public class OrchestratorClient : IDisposable
    {
        private readonly HttpClient _http;
        private string _baseUrl;
        private bool _disposed;

        public OrchestratorClient(string baseUrl)
        {
            _baseUrl = NormalizeUrl(baseUrl);
            _http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
        }

        public string BaseUrl
        {
            get => _baseUrl;
            set => _baseUrl = NormalizeUrl(value);
        }

        // Tolerate Python's non-standard JSON numerics (Infinity, NaN) — Python's
        // stdlib json emits these by default, and a stray float('inf') in the
        // dashboard payload would otherwise crash the whole panel.
        private static readonly JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            FloatParseHandling = FloatParseHandling.Double,
            FloatFormatHandling = FloatFormatHandling.DefaultValue,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        public async Task<OrchestratorStatus> GetStatusAsync(CancellationToken ct = default)
        {
            var json = await GetStringAsync("/api/status", ct).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<OrchestratorStatus>(json, _settings)
                ?? new OrchestratorStatus();
        }

        public async Task<List<OrchestratorEvent>> GetEventsAsync(int limit = 50, CancellationToken ct = default)
        {
            var json = await GetStringAsync($"/api/events?limit={limit}", ct).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<List<OrchestratorEvent>>(json, _settings)
                ?? new List<OrchestratorEvent>();
        }

        /// <summary>
        /// Trigger an E-STOP on the running Conductor. Returns true on a 2xx
        /// response, false (with no throw) on any other outcome so the UI
        /// can show a clear inline error.
        /// </summary>
        public async Task<bool> RequestEstopAsync(CancellationToken ct = default)
        {
            try
            {
                using var content = new StringContent("{}", Encoding.UTF8, "application/json");
                using var response = await _http.PostAsync(_baseUrl + "/api/estop", content, ct)
                    .ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> GetStringAsync(string path, CancellationToken ct)
        {
            using var response = await _http.GetAsync(_baseUrl + path, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return "http://127.0.0.1:8765";
            return url.TrimEnd('/');
        }

        public void Dispose()
        {
            if (_disposed) return;
            _http.Dispose();
            _disposed = true;
        }
    }
}
