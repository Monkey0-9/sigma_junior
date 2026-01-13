using System;
using System.Collections.Generic;
using System.Linq;

namespace Hft.Strategies
{
    /// <summary>
    /// Represents an alpha signal with associated metrics.
    /// </summary>
    public class AlphaSignal
    {
        public string Name { get; set; } = string.Empty;
        public double CurrentForecast { get; set; } // Normalized signal [-1, 1]
        public double HistoricalSharpe { get; set; }
        public double Weight { get; set; }
        public double ConfidenceScore { get; set; } = 1.0;
    }

    /// <summary>
    /// Institutional Bayesian Alpha Ensemble Optimizer.
    /// Weights multiple alpha signals based on historical confidence and real-time performance.
    /// Aligned with Renaissance Technologies' statistical research philosophy.
    /// </summary>
    public class BayesianAlphaOptimizer
    {
        private readonly List<AlphaSignal> _signals = new();
        private readonly object _lock = new();

        public void RegisterSignal(AlphaSignal signal)
        {
            lock (_lock)
            {
                _signals.Add(signal);
            }
        }

        /// <summary>
        /// Computes the ensemble signal using Bayesian posterior weights.
        /// Signals with higher Historical Sharpe and higher real-time confidence receive more weight.
        /// </summary>
        public double ComputeEnsembleSignal()
        {
            lock (_lock)
            {
                if (_signals.Count == 0) return 0;

                double totalConfidence = _signals.Sum(s => s.HistoricalSharpe * s.ConfidenceScore);
                if (totalConfidence <= 0) return 0;

                double ensemble = 0;
                foreach (var signal in _signals)
                {
                    double bayesianWeight = (signal.HistoricalSharpe * signal.ConfidenceScore) / totalConfidence;
                    ensemble += signal.CurrentForecast * bayesianWeight;
                    signal.Weight = bayesianWeight; // Update for attribution
                }

                return Math.Clamp(ensemble, -1.0, 1.0);
            }
        }

        /// <summary>
        /// Updates signal confidence based on recent prediction accuracy (Feedback Loop).
        /// </summary>
        public void UpdateConfidence(string signalName, double accuracyScore)
        {
            lock (_lock)
            {
                var signal = _signals.FirstOrDefault(s => s.Name == signalName);
                if (signal != null)
                {
                    // Exponential Moving Average update for confidence
                    signal.ConfidenceScore = (signal.ConfidenceScore * 0.9) + (accuracyScore * 0.1);
                }
            }
        }
    }
}

