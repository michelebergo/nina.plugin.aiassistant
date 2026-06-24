using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace NINA.Plugin.AIAssistant.Orchestrator
{
    /// <summary>
    /// Lightweight row used in the panel's event list — flattens the raw
    /// OrchestratorEvent so the data template can bind without JObject gymnastics.
    /// </summary>
    public class OrchestratorEventRow
    {
        public string Timestamp { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Summary { get; set; } = "";
    }

    public class OrchestratorStatusViewModel : ObservableObject, IDisposable
    {
        private OrchestratorClient _client;
        private readonly DispatcherTimer _timer;
        private CancellationTokenSource? _pollCts;
        private bool _disposed;

        public OrchestratorStatusViewModel(string baseUrl, int pollIntervalSeconds)
        {
            _client = new OrchestratorClient(baseUrl);
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(Math.Max(1, pollIntervalSeconds))
            };
            _timer.Tick += async (_, _) => await PollOnceAsync();
            EstopCommand = new AsyncRelayCommand(EstopAsync);
            RefreshCommand = new AsyncRelayCommand(PollOnceAsync);
            Events = new ObservableCollection<OrchestratorEventRow>();
        }

        #region Properties

        private string _phase = "—";
        public string Phase
        {
            get => _phase;
            private set => SetProperty(ref _phase, value);
        }

        private string _targetSummary = "";
        public string TargetSummary
        {
            get => _targetSummary;
            private set => SetProperty(ref _targetSummary, value);
        }

        private string _budgetText = "";
        public string BudgetText
        {
            get => _budgetText;
            private set => SetProperty(ref _budgetText, value);
        }

        private double _budgetPercent;
        public double BudgetPercent
        {
            get => _budgetPercent;
            private set => SetProperty(ref _budgetPercent, value);
        }

        private string _budgetState = "normal";
        public string BudgetState
        {
            get => _budgetState;
            private set => SetProperty(ref _budgetState, value);
        }

        private bool _hasBudget;
        public bool HasBudget
        {
            get => _hasBudget;
            private set => SetProperty(ref _hasBudget, value);
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            private set => SetProperty(ref _isConnected, value);
        }

        private string _connectionError = "";
        public string ConnectionError
        {
            get => _connectionError;
            private set => SetProperty(ref _connectionError, value);
        }

        public ObservableCollection<OrchestratorEventRow> Events { get; }

        public AsyncRelayCommand EstopCommand { get; }
        public AsyncRelayCommand RefreshCommand { get; }

        #endregion

        #region Public lifecycle

        public void Start()
        {
            if (_disposed) return;
            _timer.Start();
            // Fire-and-forget the first poll so the panel isn't blank for `interval` seconds.
            _ = PollOnceAsync();
        }

        public void Stop()
        {
            _timer.Stop();
            _pollCts?.Cancel();
        }

        /// <summary>
        /// Update the URL or poll interval without recreating the view model.
        /// </summary>
        public void Reconfigure(string baseUrl, int pollIntervalSeconds)
        {
            _client.BaseUrl = baseUrl;
            _timer.Interval = TimeSpan.FromSeconds(Math.Max(1, pollIntervalSeconds));
        }

        #endregion

        #region Polling

        private async Task PollOnceAsync()
        {
            if (_disposed) return;
            _pollCts?.Cancel();
            _pollCts = new CancellationTokenSource();
            var ct = _pollCts.Token;
            try
            {
                var status = await _client.GetStatusAsync(ct).ConfigureAwait(true);
                var events = await _client.GetEventsAsync(15, ct).ConfigureAwait(true);
                ApplyStatus(status);
                ApplyEvents(events);
                IsConnected = true;
                ConnectionError = "";
            }
            catch (OperationCanceledException)
            {
                // Expected on reconfigure / disposal — ignore.
            }
            catch (Exception ex)
            {
                IsConnected = false;
                ConnectionError = ex.Message;
            }
        }

        private void ApplyStatus(OrchestratorStatus status)
        {
            Phase = status.Phase ?? "—";

            if (status.Session != null)
            {
                var seq = status.Session.SequenceFile ?? "(planner-picked)";
                TargetSummary = $"session #{status.Session.Id} · {seq}";
            }
            else
            {
                TargetSummary = "no active session";
            }

            if (status.Budget != null)
            {
                HasBudget = true;
                BudgetState = status.Budget.State ?? "normal";
                var spent = status.Budget.SpentUsd ?? 0.0;
                var budget = status.Budget.BudgetUsd ?? 0.0;
                BudgetText = budget > 0
                    ? $"${spent:F2} / ${budget:F2}"
                    : $"${spent:F2} (no cap)";
                BudgetPercent = budget > 0
                    ? Math.Min(100.0, (spent / budget) * 100.0)
                    : 0;
            }
            else
            {
                HasBudget = false;
                BudgetText = "";
                BudgetPercent = 0;
            }
        }

        private void ApplyEvents(List<OrchestratorEvent> events)
        {
            Events.Clear();
            foreach (var ev in events)
            {
                var ts = ev.Timestamp ?? "";
                if (ts.Length >= 19) ts = ts.Substring(0, 19).Replace('T', ' ');

                var payload = ev.Payload?.ToString(Newtonsoft.Json.Formatting.None) ?? "";
                if (payload.Length > 160) payload = payload.Substring(0, 159) + "…";

                Events.Add(new OrchestratorEventRow
                {
                    Timestamp = ts,
                    Kind = ev.Kind ?? "",
                    Summary = payload
                });
            }
        }

        #endregion

        #region Commands

        private async Task EstopAsync()
        {
            var confirm = MessageBox.Show(
                "E-STOP will stop the running sequence, close the dome, park the mount, " +
                "warm the camera, and end the session.\n\nProceed?",
                "Confirm E-STOP",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var ok = await _client.RequestEstopAsync().ConfigureAwait(true);
                if (!ok)
                {
                    ConnectionError = "E-STOP request was rejected by the orchestrator.";
                    Logger.Warning("OrchestratorStatusVM: E-STOP request rejected by " + _client.BaseUrl);
                }
                else
                {
                    Logger.Info("OrchestratorStatusVM: E-STOP requested via dashboard API");
                }
            }
            catch (Exception ex)
            {
                ConnectionError = "E-STOP failed: " + ex.Message;
                Logger.Error("OrchestratorStatusVM E-STOP failed: " + ex);
            }
            // Immediate refresh so the human sees the phase transition.
            await PollOnceAsync().ConfigureAwait(true);
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            _pollCts?.Cancel();
            _client.Dispose();
        }
    }
}
