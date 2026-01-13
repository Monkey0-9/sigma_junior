using System;
using System.Collections.Concurrent;
using System.Linq;
using Hft.Core;

namespace Hft.Risk
{
    /// <summary>
    /// Institutional Portfolio Risk Engine.
    /// Aggregates risk across multi-asset classes and factor exposures.
    /// Aligned with BlackRock Aladdin risk decomposition.
    /// GRANDMASTER: Uses auto-properties from PositionSnapshot.
    /// </summary>
    public class PortfolioRiskEngine
    {
        private readonly ConcurrentDictionary<long, PositionSnapshot> _positions = new();
        private readonly IEventLogger _logger;
        private readonly RiskLimits _globalLimits;

        public PortfolioRiskEngine(RiskLimits limits, IEventLogger logger)
        {
            _globalLimits = limits;
            _logger = logger;
        }

        public void UpdatePosition(PositionSnapshot snap)
        {
            ArgumentNullException.ThrowIfNull(snap);
            _positions.AddOrUpdate(snap.InstrumentId, snap, (id, old) => snap);
            CheckPortfolioLimits();
        }

        private void CheckPortfolioLimits()
        {
            double totalNetValue = 0;
            double totalPnL = 0;
            FactorExposure aggregateFactors = new FactorExposure();

            foreach (var pos in _positions.Values)
            {
                double netValue = pos.NetPosition * pos.AvgEntryPrice;
                totalNetValue += Math.Abs(netValue);
                totalPnL += pos.TotalPnL;

                // Simple additive factor attribution (linear approximation)
                aggregateFactors.Beta += pos.Factors.Beta * netValue;
                aggregateFactors.Volatility += pos.Factors.Volatility * netValue;
                aggregateFactors.Liquidity += pos.Factors.Liquidity * netValue;
            }

            // Normalizing factors by total exposure
            if (totalNetValue > 0)
            {
                aggregateFactors.Beta /= totalNetValue;
                aggregateFactors.Volatility /= totalNetValue;
                aggregateFactors.Liquidity /= totalNetValue;
            }

            // 1. Drawdown Monitoring
            if (totalPnL < -_globalLimits.DailyLossLimit)
            {
                _logger.LogRiskEvent("Portfolio", "ALARM", $"Total PnL {totalPnL:F2} below limit -{_globalLimits.DailyLossLimit}");
                _globalLimits.KillSwitchActive = true; // Auto-Kill on portfolio-wide breach
            }

            // 2. Factor Concentration Check (Aladdin-style)
            if (Math.Abs(aggregateFactors.Beta) > 1.5) // Portfolio Beta > 1.5 is a warning
            {
                _logger.LogRiskEvent("FactorExposure", "WARNING", $"Portfolio Beta {aggregateFactors.Beta:F2} exceeds risk tolerance");
            }
        }

        public static FactorExposure PortfolioExposure { get; }
    }
}

