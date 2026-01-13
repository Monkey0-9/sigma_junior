using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hft.Core
{
    /// <summary>
    /// Monitors system health and SLO compliance using CentralMetricsStore.
    /// Triggers alerts when thresholds are breached.
    /// </summary>
    public sealed class HealthMonitor : IDisposable
    {
        private readonly CentralMetricsStore _metrics;
        private readonly IEventLogger _logger;
        private readonly CancellationTokenSource _cts = new();
        private Task? _monitorTask;
        private bool _disposed;

        // SLO Thresholds
        public double MaxP99LatencyLimit { get; set; } = 500.0; // 500us
        public double MinFillRateLimit { get; set; } = 0.5; // 50%
        public double MaxRiskRejectRate { get; set; } = 0.05; // 5%

        public HealthMonitor(CentralMetricsStore metrics, IEventLogger logger)
        {
            _metrics = metrics;
            _logger = logger;
        }

        public void Start()
        {
            _monitorTask = Task.Run(MonitorLoop, _cts.Token);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Background monitor loop must continue on non-fatal errors")]
        private async Task MonitorLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    CheckSLOs();
                    await Task.Delay(10000, _cts.Token).ConfigureAwait(false); // Check every 10 seconds
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError("HealthMonitor", $"Error in monitor loop: {ex.Message}");
                }
            }
        }

        private void CheckSLOs()
        {
            // Check Latency SLO
            double p99 = _metrics.GetGauge("routing_latency_p99");
            if (p99 > MaxP99LatencyLimit)
            {
                _logger.LogWarning("HEALTH_BREACH", $"P99 Latency SLO breached: {p99:F2}us > {MaxP99LatencyLimit}us");
            }

            // Check Error Rate SLO
            long totalRouted = _metrics.GetCounter("orders_routed_total").Get();
            long riskRejects = _metrics.GetCounter("risk_rejected_total").Get();
            if (totalRouted > 0)
            {
                double rejectRate = (double)riskRejects / totalRouted;
                if (rejectRate > MaxRiskRejectRate)
                {
                    _logger.LogError("HEALTH_BREACH", $"Risk rejection rate high: {rejectRate:P2} > {MaxRiskRejectRate:P2}");
                }
            }
        }

        public void Stop()
        {
            _cts.Cancel();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _cts.Dispose();
        }
    }
}
