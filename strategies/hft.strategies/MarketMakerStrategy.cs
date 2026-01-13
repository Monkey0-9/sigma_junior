using System;
using System.Runtime.CompilerServices;
using Hft.Core;

namespace Hft.Strategies
{
    /// <summary>
    /// Institutional Market Maker with Order Book Imbalance.
    /// Phase 8: Strategic Evolution.
    /// </summary>
    public class MarketMakerStrategy : IStrategy
    {
        private readonly LockFreeRingBuffer<Order> _orderRing;
        private readonly MetricsCounter _ordersGenerated;
        private readonly IPositionSnapshotReader _position;
        private readonly SignalQualityManager _qualityManager;
        private readonly double _spread;
        private readonly double _qty;

        public MarketMakerStrategy(
            LockFreeRingBuffer<Order> orderRing,
            MetricsCounter ordersGenerated,
            IPositionSnapshotReader position,
            SignalQualityManager qualityManager,
            double spread = 0.02,
            double qty = 10)
        {
            _orderRing = orderRing;
            _ordersGenerated = ordersGenerated;
            _position = position;
            _qualityManager = qualityManager;
            _spread = spread;
            _qty = qty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnTick(ref MarketDataTick tick)
        {
            double mid = tick.MidPrice;
            if (mid <= 0) return;

            // Phase 8: Order Book Imbalance Alpha
            double bidVol = tick.Bid1.Size + tick.Bid2.Size;
            double askVol = tick.Ask1.Size + tick.Ask2.Size;
            double imbalance = (bidVol + askVol) > 0 ? (bidVol - askVol) / (bidVol + askVol) : 0;

            // Auto-throttle child orders based on signal quality
            double throttle = _qualityManager.GetThrottleFactor("OrderBookImbalance");
            double currentQty = _qty * throttle;

            if (currentQty <= 0) return;

            // Skew the mid price 
            double skewedMid = mid + (imbalance * 0.01);

            double myBid = skewedMid - _spread / 2.0;
            double myAsk = skewedMid + _spread / 2.0;

            // Inventory Management
            double currentPos = _position.NetPosition;
            if (currentPos > 100) { myBid -= 0.01; myAsk -= 0.01; }
            if (currentPos < -100) { myBid += 0.01; myAsk += 0.01; }

            // Emit Bid
            var bidOrder = new Order(0, tick.InstrumentId, OrderSide.Buy, myBid, currentQty, DateTime.UtcNow.Ticks, 0);
            if (_orderRing.TryWrite(in bidOrder)) _ordersGenerated.Increment();

            // Emit Ask
            var askOrder = new Order(0, tick.InstrumentId, OrderSide.Sell, myAsk, currentQty, DateTime.UtcNow.Ticks, 0);
            if (_orderRing.TryWrite(in askOrder)) _ordersGenerated.Increment();

            // Note: In a real system, we'd record prediction/outcome asynchronously to update SignalQualityManager
        }
    }
}
