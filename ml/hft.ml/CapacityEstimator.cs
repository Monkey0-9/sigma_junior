// ML Alpha Integration - Capacity Estimator
// Part E: Strategy Lifecycle & Governance
// Estimates strategy capacity based on liquidity, AUM, and market impact

using System;
using System.Collections.Generic;
using System.Linq;

namespace Hft.Ml
{
    /// <summary>
    /// Market liquidity snapshot at a point in time.
    /// </summary>
    public sealed record LiquiditySnapshot
    {
        /// <summary>Instrument symbol</summary>
        public string Symbol { get; init; } = string.Empty;
        
        /// <summary>Daily volume in contracts/shares</summary>
        public long DailyVolume { get; init; }
        
        /// <summary>Bid-ask spread in basis points</summary>
        public double SpreadBps { get; init; }
        
        /// <summary>Average daily turnover in currency</summary>
        public decimal DailyTurnover { get; init; }
        
        /// <summary>Market cap or notional value</summary>
        public decimal MarketCap { get; init; }
        
        /// <summary>Volatility (annualized)</summary>
        public double Volatility { get; init; }
        
        /// <summary>Snapshot timestamp</summary>
        public DateTime Timestamp { get; init; }
    }

    /// <summary>
    /// Capacity estimate for a strategy.
    /// </summary>
    public sealed record CapacityEstimate
    {
        /// <summary>Strategy identifier</summary>
        public string StrategyId { get; init; } = string.Empty;
        
        /// <summary>Estimated maximum AUM in currency</summary>
        public decimal MaximumAum { get; init; }
        
        /// <summary>Current AUM in currency</summary>
        public decimal CurrentAum { get; init; }
        
        /// <summary>Remaining capacity in currency</summary>
        public decimal RemainingCapacity { get; init; }
        
        /// <summary>Capacity utilization percentage</summary>
        public double CapacityUtilization { get; init; }
        
        /// <summary>Constraint limiting capacity (liquidity, volatility, etc)</summary>
        public string LimitingConstraint { get; init; } = string.Empty;
        
        /// <summary>Recommended position size as percent of AUM</summary>
        public double RecommendedPositionSizePercent { get; init; }
        
        /// <summary>Estimated market impact per unit traded (bps)</summary>
        public double EstimatedImpactBps { get; init; }
        
        /// <summary>Cost of scaling (basis points)</summary>
        public double ScalingCostBps { get; init; }
        
        /// <summary>Estimate timestamp</summary>
        public DateTime EstimateTime { get; init; }
        
        /// <summary>Confidence in estimate (0-1)</summary>
        public double Confidence { get; init; }
    }

    /// <summary>
    /// Capacity stress test result.
    /// </summary>
    public sealed record CapacityStressTest
    {
        /// <summary>Scenario description (e.g., "AUM doubles")</summary>
        public string Scenario { get; init; } = string.Empty;
        
        /// <summary>Hypothetical AUM level</summary>
        public decimal HypotheticalAum { get; init; }
        
        /// <summary>Estimated average position size</summary>
        public decimal AvgPositionSize { get; init; }
        
        /// <summary>Estimated market impact per trade</summary>
        public double MarketImpactBps { get; init; }
        
        /// <summary>Estimated trading costs as percent of returns</summary>
        public double CostAsPercentOfReturns { get; init; }
        
        /// <summary>Viability assessment (viable, constrained, infeasible)</summary>
        public string Viability { get; init; } = string.Empty;
        
        /// <summary>Test timestamp</summary>
        public DateTime TestTime { get; init; }
    }

    /// <summary>
    /// Estimates and monitors strategy capacity.
    /// 
    /// Approach:
    /// 1. Market liquidity analysis (daily volume, spreads, turnover)
    /// 2. Position sizing constraints (max % of daily volume)
    /// 3. Market impact modeling (Almgren-Chriss or linear impact)
    /// 4. Slippage estimation
    /// 5. Correlation with other exposures
    /// 
    /// Constraints:
    /// - Max 5% of daily volume per symbol
    /// - Max 25% of AUM in single symbol
    /// - Volatility constraints (VaR limits)
    /// - Liquidity requirements (minimum bid-ask liquidity)
    /// 
    /// Thread safety: This is a reference type; use synchronized access if needed.
    /// </summary>
    public sealed class CapacityEstimator : IDisposable
    {
        private readonly Dictionary<string, List<LiquiditySnapshot>> _liquidityHistory;
        private readonly Dictionary<string, CapacityEstimate?> _capacityCache;
        
        /// <summary>Maximum percentage of daily volume per symbol</summary>
        public double MaxPercentOfDailyVolume { get; set; } = 0.05; // 5%
        
        /// <summary>Maximum portfolio concentration per symbol</summary>
        public double MaxPortfolioConcentration { get; set; } = 0.25; // 25%
        
        /// <summary>Assumed market impact coefficient (square-root law)</summary>
        public double MarketImpactCoefficient { get; set; } = 0.50; // 50 bps per sqrt(10% volume)
        
        /// <summary>Assumed fixed slippage per trade</summary>
        public double FixedSlippageBps { get; set; } = 1.0; // 1 bp
        
        /// <summary>Minimum required liquidity (in bps of spread)</summary>
        public double MinimumSpreadBps { get; set; } = 0.5; // 0.5 bp minimum acceptable spread
        
        /// <summary>Maximum VaR as percent of AUM</summary>
        public double MaxVaRPercent { get; set; } = 0.02; // 2% daily VaR

        public CapacityEstimator()
        {
            _liquidityHistory = new Dictionary<string, List<LiquiditySnapshot>>();
            _capacityCache = new Dictionary<string, CapacityEstimate?>();
        }

        /// <summary>
        /// Record liquidity snapshot for capacity analysis.
        /// </summary>
        public void RecordLiquiditySnapshot(LiquiditySnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentException.ThrowIfNullOrEmpty(snapshot.Symbol);

            if (!_liquidityHistory.TryGetValue(snapshot.Symbol, out var history))
            {
                history = new List<LiquiditySnapshot>();
                _liquidityHistory[snapshot.Symbol] = history;
            }

            history.Add(snapshot);

            // Keep only last 30 days of history
            var cutoff = DateTime.UtcNow.AddDays(-30);
            if (_liquidityHistory.TryGetValue(snapshot.Symbol, out var historyData))
            {
                _liquidityHistory[snapshot.Symbol] = historyData.Where(s => s.Timestamp >= cutoff).ToList();
            }
        }

        /// <summary>
        /// Estimate capacity for a strategy.
        /// </summary>
        public CapacityEstimate EstimateCapacity(
            string strategyId,
            decimal currentAum,
            IReadOnlyList<string> symbols,
            double expectedAnnualizedReturns = 0.15)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);
            ArgumentNullException.ThrowIfNull(symbols);

            if (symbols.Count == 0)
            {
                return new CapacityEstimate
                {
                    StrategyId = strategyId,
                    CurrentAum = currentAum,
                    MaximumAum = currentAum,
                    RemainingCapacity = 0,
                    CapacityUtilization = 1.0,
                    LimitingConstraint = "No symbols specified",
                    EstimateTime = DateTime.UtcNow,
                    Confidence = 0.0
                };
            }

            // Calculate capacity constraints from each symbol
            var capacities = new List<decimal>();
            var constraints = new List<string>();

            foreach (var symbol in symbols)
            {
                var (capacity, constraint) = CalculateSymbolCapacity(symbol);
                capacities.Add(capacity);
                if (!string.IsNullOrEmpty(constraint))
                {
                    constraints.Add($"{symbol}: {constraint}");
                }
            }

            decimal maxAum = capacities.Count > 0 ? capacities.Min() : currentAum;
            decimal remainingCapacity = Math.Max(0, maxAum - currentAum);
            double utilization = maxAum > 0 ? (double)(currentAum / maxAum) : 1.0;

            // Calculate market impact cost at current scale
            double impactBps = CalculateExpectedMarketImpact(currentAum, symbols);
            
            // Calculate cost of scaling
            double scalingCost = CalculateScalingCost(currentAum, maxAum, expectedAnnualizedReturns);

            var estimate = new CapacityEstimate
            {
                StrategyId = strategyId,
                MaximumAum = maxAum,
                CurrentAum = currentAum,
                RemainingCapacity = remainingCapacity,
                CapacityUtilization = utilization,
                LimitingConstraint = constraints.Count > 0 ? string.Join("; ", constraints) : "Adequate liquidity",
                RecommendedPositionSizePercent = 1.0 / Math.Max(1, symbols.Count), // Equal weight
                EstimatedImpactBps = impactBps,
                ScalingCostBps = scalingCost,
                EstimateTime = DateTime.UtcNow,
                Confidence = CalculateEstimateConfidence(symbols)
            };

            _capacityCache[strategyId] = estimate;
            return estimate;
        }

        /// <summary>
        /// Run capacity stress tests.
        /// </summary>
        public IReadOnlyList<CapacityStressTest> RunCapacityStressTests(
            string strategyId,
            decimal baselineAum,
            IReadOnlyList<string> symbols,
            double expectedReturns)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);
            ArgumentNullException.ThrowIfNull(symbols);

            var tests = new List<CapacityStressTest>();

            // Test scenarios: 1.5x, 2x, 3x, 5x AUM scaling
            decimal[] scenarios = { baselineAum * 1.5m, baselineAum * 2m, baselineAum * 3m, baselineAum * 5m };
            string[] descriptions = { "50% AUM increase", "2x AUM", "3x AUM", "5x AUM" };

            for (int i = 0; i < scenarios.Length; i++)
            {
                var estimate = EstimateCapacity(
                    $"{strategyId}_stress_{i}",
                    scenarios[i],
                    symbols,
                    expectedReturns);

                double costAsPercentOfReturns = (estimate.ScalingCostBps / 10000.0) / 
                    Math.Max(0.001, expectedReturns / 252.0); // Convert to daily

                string viability = costAsPercentOfReturns < 0.5 
                    ? "Viable"
                    : costAsPercentOfReturns < 1.0
                    ? "Constrained"
                    : "Infeasible";

                tests.Add(new CapacityStressTest
                {
                    Scenario = descriptions[i],
                    HypotheticalAum = scenarios[i],
                    AvgPositionSize = scenarios[i] / symbols.Count,
                    MarketImpactBps = CalculateExpectedMarketImpact(scenarios[i], symbols),
                    CostAsPercentOfReturns = costAsPercentOfReturns,
                    Viability = viability,
                    TestTime = DateTime.UtcNow
                });
            }

            return tests.AsReadOnly();
        }

        /// <summary>
        /// Get capacity for a strategy (cached).
        /// </summary>
        public CapacityEstimate? GetCapacityEstimate(string strategyId)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);
            _capacityCache.TryGetValue(strategyId, out var estimate);
            return estimate;
        }

        private (decimal capacity, string constraint) CalculateSymbolCapacity(string symbol)
        {
            if (!_liquidityHistory.TryGetValue(symbol, out var snapshots) || snapshots.Count == 0)
            {
                return (decimal.MaxValue, "No liquidity data");
            }

            var recent = snapshots.Last();

            // Volume constraint: Max 5% of daily volume
            decimal volumeCapacity = (decimal)recent.DailyVolume * (decimal)MaxPercentOfDailyVolume * recent.SpreadBps switch
            {
                >= 2.0 => 1.0m, // Wide spread = lower capacity
                >= 1.0 => 0.8m,
                >= 0.5 => 0.6m,
                _ => 0.4m
            };

            // Spread constraint: Penalize tight spreads (harder to exit)
            string spreadConstraint = "";
            if (recent.SpreadBps < MinimumSpreadBps)
            {
                spreadConstraint = $"Insufficient liquidity (spread {recent.SpreadBps} bps)";
                volumeCapacity *= 0.5m;
            }
            else if (recent.SpreadBps > 5.0)
            {
                spreadConstraint = $"Wide spreads ({recent.SpreadBps} bps) limit capacity";
                volumeCapacity *= 0.7m;
            }

            return (Math.Max(0, volumeCapacity), spreadConstraint);
        }

        private double CalculateExpectedMarketImpact(decimal aumSize, IReadOnlyList<string> symbols)
        {
            if (symbols.Count == 0)
                return 0;

            double totalImpact = 0;

            foreach (var symbol in symbols)
            {
                if (!_liquidityHistory.TryGetValue(symbol, out var snapshots) || snapshots.Count == 0)
                {
                    continue;
                }

                var recent = snapshots.Last();
                
                // Position size relative to daily volume
                double posSize = (double)(aumSize / (decimal)symbols.Count) / (double)(recent.DailyTurnover > 0 ? recent.DailyTurnover : 1m);
                
                // Market impact using square-root law: impact = coefficient * sqrt(% of volume)
                double sqrtVolumePercent = Math.Sqrt(Math.Max(0, Math.Min(1.0, posSize)));
                double impact = MarketImpactCoefficient * sqrtVolumePercent + FixedSlippageBps;

                totalImpact += impact;
            }

            return totalImpact / symbols.Count;
        }

        private static double CalculateScalingCost(decimal currentAum, decimal maxAum, double expectedReturns)
        {
            if (maxAum <= currentAum)
                return 0;

            // Scaling cost increases with AUM: cost = fixed + variable * AUM_increase
            double aumRatio = (double)(maxAum / Math.Max(1, currentAum));
            double scalingMultiplier = Math.Pow(aumRatio, 1.5) - 1.0; // Convex cost function
            
            return scalingMultiplier * 50.0; // 50 bps per doubling, scaled by ratio
        }

        private double CalculateEstimateConfidence(IReadOnlyList<string> symbols)
        {
            int dataPoints = 0;
            int totalSymbols = 0;

            foreach (var symbol in symbols)
            {
                if (_liquidityHistory.TryGetValue(symbol, out var snapshots))
                {
                    dataPoints += snapshots.Count;
                }
                totalSymbols++;
            }

            // Confidence: higher with more data and more symbols
            double dataConfidence = Math.Min(1.0, dataPoints / 30.0); // 30 days = full confidence
            double diversification = Math.Min(1.0, totalSymbols / 10.0); // 10+ symbols = full confidence

            return dataConfidence * diversification;
        }

        public void Dispose()
        {
            _liquidityHistory.Clear();
            _capacityCache.Clear();
        }
    }
}
