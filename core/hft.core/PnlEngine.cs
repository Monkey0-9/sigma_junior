using System;

namespace Hft.Core
{
    public class PnlEngine
    {
        private readonly PositionSnapshot _snapshot;
        private readonly IEventLogger? _logger;
        private double _currentMarketPrice;

        public PnlEngine(PositionSnapshot snapshot, IEventLogger? logger = null)
        {
            _snapshot = snapshot;
            _logger = logger;
        }

        public void OnFill(Fill fill)
        {
            double signedQty = fill.Side == OrderSide.Buy ? fill.Quantity : -fill.Quantity;

            double oldPos = _snapshot.NetPosition;
            double newPos = oldPos + signedQty;

            if (oldPos != 0 && Math.Sign(oldPos) != Math.Sign(signedQty))
            {
                double closingQty = Math.Min(Math.Abs(signedQty), Math.Abs(oldPos)) * Math.Sign(signedQty);
                double pnl = (oldPos > 0) ? (fill.Price - _snapshot.AvgEntryPrice) * Math.Abs(closingQty)
                                          : (_snapshot.AvgEntryPrice - fill.Price) * Math.Abs(closingQty);
                _snapshot.RealizedPnL = _snapshot.RealizedPnL + pnl;
            }

            if (newPos != 0)
            {
                if (oldPos == 0 || Math.Sign(oldPos) == Math.Sign(signedQty))
                {
                     double totalCost = (_snapshot.AvgEntryPrice * Math.Abs(oldPos)) + (fill.Price * Math.Abs(signedQty));
                     _snapshot.AvgEntryPrice = totalCost / Math.Abs(newPos);
                }
            }
            else
            {
                _snapshot.AvgEntryPrice = 0;
            }

            _snapshot.NetPosition = newPos;
            _logger?.LogPnlUpdate(_snapshot.InstrumentId, newPos, _snapshot.RealizedPnL, _snapshot.UnrealizedPnL);
            MarkToMarket(_currentMarketPrice);
        }

        public void MarkToMarket(double currentPrice)
        {
            _currentMarketPrice = currentPrice;
            double netPos = _snapshot.NetPosition;
            double avgPx = _snapshot.AvgEntryPrice;

            if (netPos == 0)
            {
                _snapshot.UnrealizedPnL = 0;
            }
            else
            {
                if (netPos > 0)
                    _snapshot.UnrealizedPnL = (currentPrice - avgPx) * netPos;
                else
                    _snapshot.UnrealizedPnL = (avgPx - currentPrice) * Math.Abs(netPos);
            }
            _logger?.LogPnlUpdate(_snapshot.InstrumentId, netPos, _snapshot.RealizedPnL, _snapshot.UnrealizedPnL);
        }
    }
}

