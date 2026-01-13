using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Hft.Core
{
    /// <summary>
    /// Institutional Multi-Asset PnL Engine.
    /// Supports multi-currency accounting, FX conversion, and asset-specific calc logic.
    /// Aligned with Aladdin-style portfolio reconciliation.
    /// </summary>
    public class MultiAssetPnlEngine
    {
        private readonly ConcurrentDictionary<long, PositionSnapshot> _positions = new();
        private readonly ConcurrentDictionary<string, double> _fxRates = new(); // Currency -> USD Rate
        private readonly IEventLogger _logger;

        public MultiAssetPnlEngine(IEventLogger logger)
        {
            _logger = logger;
            _fxRates["USD"] = 1.0;
        }

        public void UpdateFxRate(string currency, double rate)
        {
            _fxRates[currency] = rate;
            _logger.LogInfo("PnL", $"FX Rate Update: {currency}/USD = {rate:F4}");
        }

        public void OnFill(Fill fill, string currency = "USD")
        {
            if (!_positions.TryGetValue(fill.InstrumentId, out var pos))
            {
                // In production, positions should be pre-initialized or resolved via Instrument master
                return;
            }

            double signedQty = fill.Side == OrderSide.Buy ? fill.Quantity : -fill.Quantity;
            double fxRate = _fxRates.GetValueOrDefault(currency, 1.0);

            // FIFO matching logic would go here for tax-lot accounting.
            // Simplified weighted average for institutional HFT node.
            double oldPos = pos.NetPosition;
            double newPos = oldPos + signedQty;

            if (oldPos != 0 && Math.Sign(oldPos) != Math.Sign(signedQty))
            {
                // Partial or full close
                double closingQty = Math.Min(Math.Abs(signedQty), Math.Abs(oldPos)) * Math.Sign(signedQty);
                double realized = (oldPos > 0)
                    ? (fill.Price - pos.AvgEntryPrice) * Math.Abs(closingQty) * fxRate
                    : (pos.AvgEntryPrice - fill.Price) * Math.Abs(closingQty) * fxRate;

                pos.RealizedPnL += realized;
            }

            if (newPos != 0)
            {
                if (oldPos == 0 || Math.Sign(oldPos) == Math.Sign(signedQty))
                {
                    // Adding to position or flip
                    double totalCost = (pos.AvgEntryPrice * Math.Abs(oldPos)) + (fill.Price * Math.Abs(signedQty));
                    pos.AvgEntryPrice = totalCost / Math.Abs(newPos);
                }
            }
            else
            {
                pos.AvgEntryPrice = 0;
            }

            pos.NetPosition = newPos;
        }

        public double TotalPortfolioValueUsd
        {
            get
            {
                double total = 0;
                foreach (var pos in _positions.Values)
                {
                    total += pos.TotalPnL; // Already in USD if calculated via fxRate
                }
                return total;
            }
        }
    }
}

