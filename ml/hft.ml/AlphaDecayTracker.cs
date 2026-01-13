// ML Alpha Integration - Alpha Decay Tracker
// Part E: Strategy Lifecycle & Governance
// Tracks real-time decay of strategy alpha/performance over time

using System;
using System.Collections.Generic;
using System.Linq;

namespace Hft.Ml
{
    /// <summary>
    /// Alpha decay observation at a specific point in time.
    /// </summary>
    public sealed record AlphaDecayObservation
    {
        /// <summary>Strategy identifier</summary>
        public string StrategyId { get; init; } = string.Empty;
        
        /// <summary>Observation timestamp</summary>
        public DateTime Timestamp { get; init; }
        
        /// <summary>Sharpe ratio at this observation</summary>
        public double SharpeRatio { get; init; }
        
        /// <summary>Information ratio at this observation</summary>
        public double InformationRatio { get; init; }
        
        /// <summary>Win rate at this observation</summary>
        public double WinRate { get; init; }
        
        /// <summary>PnL for the observation window</summary>
        public double PnL { get; init; }
        
        /// <summary>Trade count in observation window</summary>
        public long TradeCount { get; init; }
        
        /// <summary>Volatility at this observation</summary>
        public double Volatility { get; init; }
        
        /// <summary>Days since strategy started</summary>
        public int DaysSinceStart { get; init; }
    }

    /// <summary>
    /// Decay analysis result comparing in-sample vs out-of-sample performance.
    /// </summary>
    public sealed record DecayAnalysis
    {
        /// <summary>Strategy identifier</summary>
        public string StrategyId { get; init; } = string.Empty;
        
        /// <summary>In-sample (training) Sharpe ratio</summary>
        public double InSampleSharpe { get; init; }
        
        /// <summary>Out-of-sample (live) Sharpe ratio</summary>
        public double OutOfSampleSharpe { get; init; }
        
        /// <summary>Decay percentage (1.0 = 100% decay)</summary>
        public double DecayPercent { get; init; }
        
        /// <summary>Days since in-sample period ended</summary>
        public int DaysSinceInSample { get; init; }
        
        /// <summary>Monthly decay rate (percent per month)</summary>
        public double MonthlyDecayRate { get; init; }
        
        /// <summary>Is decay statistically significant</summary>
        public bool IsSignificantDecay { get; init; }
        
        /// <summary>Current trend direction (1=improving, -1=declining, 0=stable)</summary>
        public int TrendDirection { get; init; }
        
        /// <summary>Estimated half-life of alpha (days)</summary>
        public double? EstimatedHalfLifeDays { get; init; }
        
        /// <summary>Analysis notes</summary>
        public string Notes { get; init; } = string.Empty;
    }

    /// <summary>
    /// Decay alert when alpha decline exceeds thresholds.
    /// </summary>
    public sealed record DecayAlert
    {
        /// <summary>Alert severity (1=low, 5=critical)</summary>
        public int Severity { get; init; }
        
        /// <summary>Alert type (DECAY_ACCELERATING, DECAY_CRITICAL, etc)</summary>
        public string AlertType { get; init; } = string.Empty;
        
        /// <summary>Human-readable message</summary>
        public string Message { get; init; } = string.Empty;
        
        /// <summary>Alert timestamp</summary>
        public DateTime AlertTime { get; init; }
        
        /// <summary>Recommended action</summary>
        public string RecommendedAction { get; init; } = string.Empty;
    }

    /// <summary>
    /// Tracks alpha decay and out-of-sample performance degradation.
    /// 
    /// Responsibilities:
    /// - Record periodic performance observations
    /// - Compare in-sample vs out-of-sample metrics
    /// - Calculate decay rates and trends
    /// - Detect significant alpha degradation
    /// - Estimate alpha half-life
    /// - Alert on concerning decay patterns
    /// 
    /// Thread safety: This is a reference type; use synchronized access if needed.
    /// </summary>
    public sealed class AlphaDecayTracker : IDisposable
    {
        private readonly Dictionary<string, List<AlphaDecayObservation>> _observations;
        private readonly Dictionary<string, DecayAnalysis?> _decayAnalyses;
        private readonly Dictionary<string, Queue<DecayAlert>> _alerts;
        
        /// <summary>Threshold for significant decay detection (percent)</summary>
        public double SignificantDecayThreshold { get; set; } = 0.30; // 30% decay
        
        /// <summary>Threshold for critical decay (percent)</summary>
        public double CriticalDecayThreshold { get; set; } = 0.50; // 50% decay
        
        /// <summary>Minimum observations needed for decay analysis</summary>
        public int MinObservationsForAnalysis { get; set; } = 5;
        
        /// <summary>Minimum days of live trading before decay analysis</summary>
        public int MinDaysForAnalysis { get; set; } = 7;

        public AlphaDecayTracker()
        {
            _observations = new Dictionary<string, List<AlphaDecayObservation>>();
            _decayAnalyses = new Dictionary<string, DecayAnalysis?>();
            _alerts = new Dictionary<string, Queue<DecayAlert>>();
        }

        /// <summary>
        /// Record a performance observation for a strategy.
        /// </summary>
        public void RecordObservation(AlphaDecayObservation observation)
        {
            ArgumentNullException.ThrowIfNull(observation);
            ArgumentException.ThrowIfNullOrEmpty(observation.StrategyId);

            if (!_observations.TryGetValue(observation.StrategyId, out var observations))
            {
                observations = new List<AlphaDecayObservation>();
                _observations[observation.StrategyId] = observations;
            }

            observations.Add(observation);

            // Invalidate cached analysis when new observation added
            if (_decayAnalyses.TryGetValue(observation.StrategyId, out _))
            {
                _decayAnalyses[observation.StrategyId] = null;
            }
        }

        /// <summary>
        /// Analyze decay for a strategy with in-sample baseline.
        /// </summary>
        public DecayAnalysis? AnalyzeDecay(
            string strategyId,
            double inSampleSharpe,
            DateTime inSampleEndDate)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);

            if (!_observations.TryGetValue(strategyId, out var observations))
            {
                return null;
            }

            if (observations.Count < MinObservationsForAnalysis)
            {
                return null;
            }

            var liveObs = observations
                .Where(o => o.Timestamp > inSampleEndDate)
                .ToList();

            if (liveObs.Count < MinObservationsForAnalysis)
            {
                return null;
            }

            var oldestLive = liveObs.First();
            var newestLive = liveObs.Last();
            
            int daysSinceInSample = (int)(newestLive.Timestamp - inSampleEndDate).TotalDays;
            if (daysSinceInSample < MinDaysForAnalysis)
            {
                return null;
            }

            // Calculate average out-of-sample Sharpe
            double avgOutOfSampleSharpe = liveObs.Average(o => o.SharpeRatio);
            
            // Calculate decay percentage
            double decay = Math.Max(0, (inSampleSharpe - avgOutOfSampleSharpe) / Math.Max(0.001, inSampleSharpe));
            
            // Calculate monthly decay rate
            double monthlyDecay = decay / (daysSinceInSample / 30.0);

            // Detect trend (declining, stable, or improving)
            int trendDirection = 0;
            if (liveObs.Count >= 2)
            {
                double recentAvg = liveObs.TakeLast(Math.Max(1, liveObs.Count / 2)).Average(o => o.SharpeRatio);
                double earlyAvg = liveObs.Take(Math.Max(1, liveObs.Count / 2)).Average(o => o.SharpeRatio);
                
                if (recentAvg < earlyAvg * 0.95)
                    trendDirection = -1; // Declining
                else if (recentAvg > earlyAvg * 1.05)
                    trendDirection = 1; // Improving
            }

            // Estimate half-life of alpha
            double? halfLife = null;
            if (monthlyDecay > 0.01)
            {
                // Using exponential decay: S(t) = S0 * e^(-t/τ)
                // Half-life when S(t) = S0/2: τ = ln(2) / (decay_rate)
                halfLife = (Math.Log(2) / monthlyDecay) * 30; // Convert to days
            }

            // Determine if decay is significant
            bool isSignificant = decay > SignificantDecayThreshold;

            var analysis = new DecayAnalysis
            {
                StrategyId = strategyId,
                InSampleSharpe = inSampleSharpe,
                OutOfSampleSharpe = avgOutOfSampleSharpe,
                DecayPercent = decay,
                DaysSinceInSample = daysSinceInSample,
                MonthlyDecayRate = monthlyDecay,
                IsSignificantDecay = isSignificant,
                TrendDirection = trendDirection,
                EstimatedHalfLifeDays = halfLife,
                Notes = GenerateAnalysisNotes(decay, monthlyDecay, trendDirection, halfLife)
            };

            _decayAnalyses[strategyId] = analysis;

            // Check for alerts
            CheckAndGenerateAlerts(strategyId, analysis);

            return analysis;
        }

        /// <summary>
        /// Get cached decay analysis or analyze if not cached.
        /// </summary>
        public DecayAnalysis? GetDecayAnalysis(
            string strategyId,
            double inSampleSharpe,
            DateTime inSampleEndDate)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);

            if (_decayAnalyses.TryGetValue(strategyId, out var cached) && cached != null)
            {
                return cached;
            }

            return AnalyzeDecay(strategyId, inSampleSharpe, inSampleEndDate);
        }

        /// <summary>
        /// Get all observations for a strategy.
        /// </summary>
        public IReadOnlyList<AlphaDecayObservation> GetObservations(string strategyId)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);

            if (_observations.TryGetValue(strategyId, out var obs))
            {
                return obs.AsReadOnly();
            }

            return Array.Empty<AlphaDecayObservation>();
        }

        /// <summary>
        /// Get recent observations (last N days).
        /// </summary>
        public IReadOnlyList<AlphaDecayObservation> GetRecentObservations(string strategyId, int lastNDays)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);

            if (!_observations.TryGetValue(strategyId, out var obs))
            {
                return Array.Empty<AlphaDecayObservation>();
            }

            var cutoff = DateTime.UtcNow.AddDays(-lastNDays);
            return obs.Where(o => o.Timestamp >= cutoff).ToList().AsReadOnly();
        }

        /// <summary>
        /// Get active alerts for a strategy.
        /// </summary>
        public IReadOnlyList<DecayAlert> GetAlerts(string strategyId)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);

            if (_alerts.TryGetValue(strategyId, out var queue))
            {
                return queue.ToList().AsReadOnly();
            }

            return Array.Empty<DecayAlert>();
        }

        /// <summary>
        /// Clear acknowledged alerts.
        /// </summary>
        public void ClearAlerts(string strategyId)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);
            
            if (_alerts.TryGetValue(strategyId, out var alerts))
            {
                alerts.Clear();
            }
        }

        /// <summary>
        /// Check if strategy shows concerning decay pattern.
        /// </summary>
        public bool ShowsConcerningDecay(string strategyId)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);

            if (_decayAnalyses.TryGetValue(strategyId, out var analysis) && analysis != null)
            {
                return analysis.IsSignificantDecay && analysis.TrendDirection <= 0;
            }

            return false;
        }

        private void CheckAndGenerateAlerts(string strategyId, DecayAnalysis analysis)
        {
            if (!_alerts.TryGetValue(strategyId, out var queue))
            {
                queue = new Queue<DecayAlert>();
                _alerts[strategyId] = queue;
            }

            // Critical decay alert
            if (analysis.DecayPercent > CriticalDecayThreshold)
            {
                queue.Enqueue(new DecayAlert
                {
                    Severity = 5,
                    AlertType = "DECAY_CRITICAL",
                    Message = $"Critical alpha decay detected: {analysis.DecayPercent:P1} deterioration from in-sample performance",
                    AlertTime = DateTime.UtcNow,
                    RecommendedAction = "Immediately review strategy logic; consider pausing trading pending investigation"
                });
            }
            // Significant decay alert
            else if (analysis.DecayPercent > SignificantDecayThreshold)
            {
                queue.Enqueue(new DecayAlert
                {
                    Severity = 3,
                    AlertType = "DECAY_SIGNIFICANT",
                    Message = $"Significant alpha decay: {analysis.DecayPercent:P1} deterioration from in-sample performance",
                    AlertTime = DateTime.UtcNow,
                    RecommendedAction = "Analyze market regime changes; adjust parameters if needed"
                });
            }

            // Accelerating decay alert
            if (analysis.TrendDirection < 0 && analysis.MonthlyDecayRate > 0.10)
            {
                queue.Enqueue(new DecayAlert
                {
                    Severity = 4,
                    AlertType = "DECAY_ACCELERATING",
                    Message = $"Alpha decay accelerating at {analysis.MonthlyDecayRate:P1} per month",
                    AlertTime = DateTime.UtcNow,
                    RecommendedAction = "Escalate to strategy review; plan for retraining or model refresh"
                });
            }

            // Quick half-life alert
            if (analysis.EstimatedHalfLifeDays.HasValue && analysis.EstimatedHalfLifeDays < 30)
            {
                queue.Enqueue(new DecayAlert
                {
                    Severity = 4,
                    AlertType = "SHORT_ALPHA_HALFLIFE",
                    Message = $"Estimated alpha half-life is only {analysis.EstimatedHalfLifeDays:F0} days",
                    AlertTime = DateTime.UtcNow,
                    RecommendedAction = "Schedule regular retraining cycles; increase model update frequency"
                });
            }
        }

        private static string GenerateAnalysisNotes(double decay, double monthlyDecay, int trend, double? halfLife)
        {
            var notes = new List<string>();

            notes.Add($"Total decay: {decay:P1}");
            notes.Add($"Monthly decay rate: {monthlyDecay:P2}");

            if (trend > 0)
                notes.Add("Trend: Improving");
            else if (trend < 0)
                notes.Add("Trend: Declining");
            else
                notes.Add("Trend: Stable");

            if (halfLife.HasValue)
                notes.Add($"Estimated alpha half-life: {halfLife:F0} days");

            return string.Join(" | ", notes);
        }

        public void Dispose()
        {
            _observations.Clear();
            _decayAnalyses.Clear();
            _alerts.Clear();
        }
    }
}
