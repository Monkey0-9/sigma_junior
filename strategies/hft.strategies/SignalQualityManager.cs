using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Hft.Core;

namespace Hft.Strategies
{
    /// <summary>
    /// Institutional Signal Quality Manager.
    /// Monitors Information Coefficient (IC) and implements Auto-Throttling.
    /// Ensures we don't trade when predictive power decays below institutional thresholds.
    /// </summary>
    public sealed class SignalQualityManager
    {
        private sealed class SignalMetrics
        {
            public readonly ConcurrentQueue<(double Prediction, double Realization)> Samples = new();
            public double CurrentIC;
            public double ThrottleFactor = 1.0;
            public const int MinSamples = 100;
            public const double MinICThreshold = 0.02; // Institutional-grade minimum for profitable execution
        }

        private readonly ConcurrentDictionary<string, SignalMetrics> _metrics = new();
        private readonly IEventLogger _logger;

        public SignalQualityManager(IEventLogger logger)
        {
            _logger = logger;
        }

        public void RecordOutcome(string signalName, double prediction, double realization)
        {
            ArgumentNullException.ThrowIfNull(signalName);
            var m = _metrics.GetOrAdd(signalName, _ => new SignalMetrics());
            m.Samples.Enqueue((prediction, realization));

            // Keep only recent history for rolling IC
            while (m.Samples.Count > 1000) m.Samples.TryDequeue(out _);

            if (m.Samples.Count >= SignalMetrics.MinSamples)
            {
                UpdateQualityMetrics(signalName, m);
            }
        }

        private void UpdateQualityMetrics(string signalName, SignalMetrics m)
        {
            var samples = m.Samples.ToList();
            double ic = CalculateCorrelation(samples);
            m.CurrentIC = ic;

            // Auto-throttling Logic
            if (ic < SignalMetrics.MinICThreshold)
            {
                if (m.ThrottleFactor > 0)
                    _logger.LogRiskEvent("THROTTLE", "ACTIVATE", $"Signal {signalName} IC ({ic:F4}) below threshold. Throttling to 0.");
                m.ThrottleFactor = 0;
            }
            else if (ic < SignalMetrics.MinICThreshold * 2.0)
            {
                // Partial throttle (linear ramp between MinIC and 2*MinIC)
                m.ThrottleFactor = (ic - SignalMetrics.MinICThreshold) / SignalMetrics.MinICThreshold;
            }
            else
            {
                if (m.ThrottleFactor < 1.0 && ic > SignalMetrics.MinICThreshold * 2.5)
                {
                    _logger.LogInfo("THROTTLE", $"Signal {signalName} recovered (IC: {ic:F4}). Full weight restored.");
                    m.ThrottleFactor = 1.0;
                }
            }
            
            // Record IC as a gauge for monitoring
            CentralMetricsStore.Instance.SetGauge($"hft_signal_ic_{signalName.ToUpperInvariant()}", ic);
            CentralMetricsStore.Instance.SetGauge($"hft_signal_throttle_{signalName.ToUpperInvariant()}", m.ThrottleFactor);
        }

        public double GetThrottleFactor(string signalName)
        {
            return _metrics.TryGetValue(signalName, out var m) ? m.ThrottleFactor : 1.0;
        }

        private static double CalculateCorrelation(List<(double X, double Y)> samples)
        {
            if (samples == null || samples.Count == 0) return 0;
            double avgX = samples.Average(s => s.X);
            double avgY = samples.Average(s => s.Y);

            double sumXY = 0;
            double sumX2 = 0;
            double sumY2 = 0;

            foreach (var (x, y) in samples)
            {
                double dx = x - avgX;
                double dy = y - avgY;
                sumXY += dx * dy;
                sumX2 += dx * dx;
                sumY2 += dy * dy;
            }

            double denom = Math.Sqrt(sumX2 * sumY2);
            return Math.Abs(denom) < 1e-10 ? 0 : sumXY / denom;
        }
    }
}
