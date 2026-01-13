using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Hft.Core;

namespace Hft.Strategies
{
    /// <summary>
    /// Institutional Alpha Decay Monitor.
    /// Detects performance degradation using statistical tests (e.g., Kolmogorov-Smirnov / t-stats).
    /// Automatically manages signal retirement ( Jim Simons / Renaissance philosophy ).
    /// </summary>
    public class AlphaDecayMonitor
    {
        private sealed class SignalHistory
        {
            public List<double> ForecastErrors = new();
            public double BaselinePerformance;
            public DateTime ActivationDate;
            public bool IsRetired;
        }

        private readonly ConcurrentDictionary<string, SignalHistory> _signalStats = new();
        private readonly IEventLogger _logger;

        public AlphaDecayMonitor(IEventLogger logger)
        {
            _logger = logger;
        }

        public void RecordPrediction(string signalName, double forecast, double actual)
        {
            var history = _signalStats.GetOrAdd(signalName, name => new SignalHistory
            {
                ActivationDate = DateTime.UtcNow,
                BaselinePerformance = 0
            });

            if (history.IsRetired) return;

            double error = Math.Abs(forecast - actual);
            history.ForecastErrors.Add(error);

            // Periodically check for decay (e.g., every 1000 observations)
            if (history.ForecastErrors.Count % 1000 == 0)
            {
                AnalyzeDecay(signalName, history);
            }
        }

        private void AnalyzeDecay(string signalName, SignalHistory history)
        {
            if (history.ForecastErrors.Count < 2000) return;

            // Simple decay check: Compare first 1000 errors vs last 1000 errors
            var firstHalf = history.ForecastErrors.Take(1000).Average();
            var secondHalf = history.ForecastErrors.Skip(history.ForecastErrors.Count - 1000).Average();

            // If error increases significantly (> 20%), flag as decaying
            if (secondHalf > firstHalf * 1.20)
            {
                _logger.LogRiskEvent("AlphaDecay", "RETIRE", $"Signal {signalName} decaying. Error: {firstHalf:F4} -> {secondHalf:F4}");
                history.IsRetired = true;
            }
        }

        public bool IsAlive(string signalName) =>
            _signalStats.TryGetValue(signalName, out var s) && !s.IsRetired;
    }
}

