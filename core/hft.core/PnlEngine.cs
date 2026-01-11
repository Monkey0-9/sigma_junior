using System;

namespace Hft.Core
{
    public class PnlEngine
    {
        private readonly PositionSnapshot _snapshot;
        private double _currentMarketPrice;

        public PnlEngine(PositionSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public void OnFill(Fill fill)
        {
            double signedQty = fill.Side == OrderSide.Buy ? fill.Quantity : -fill.Quantity;
            double cost = signedQty * fill.Price;
            
            // Very simplified PnL logic for HFT demo
            // Real logic involves fifo/lifo matching
            
            double oldPos = _snapshot.NetPosition;
            double newPos = oldPos + signedQty;
            
            // Check if position flip or reduction
            if (oldPos != 0 && Math.Sign(oldPos) != Math.Sign(signedQty))
            {
                // Closing trade
                double closingQty = Math.Abs(signedQty) > Math.Abs(oldPos) ? -oldPos : signedQty;
                double pnl = (oldPos > 0) ? (fill.Price - _snapshot.AvgEntryPrice) * Math.Abs(closingQty) 
                                          : (_snapshot.AvgEntryPrice - fill.Price) * Math.Abs(closingQty);
                _snapshot.RealizedPnL += pnl;
            }
            
            if (newPos != 0)
            {
                // Updating Avg Entry Price is complex on flips, omitting for simple weighted avg here
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
            MarkToMarket(_currentMarketPrice);
        }

        public void MarkToMarket(double currentPrice)
        {
            _currentMarketPrice = currentPrice;
            if (_snapshot.NetPosition == 0)
            {
                _snapshot.UnrealizedPnL = 0;
            }
            else
            {
                if (_snapshot.NetPosition > 0)
                    _snapshot.UnrealizedPnL = (currentPrice - _snapshot.AvgEntryPrice) * _snapshot.NetPosition;
                else
                    _snapshot.UnrealizedPnL = (_snapshot.AvgEntryPrice - currentPrice) * Math.Abs(_snapshot.NetPosition);
            }
        }
    }
}
