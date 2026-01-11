using System;
using System.Runtime.CompilerServices;
using Hft.Core;
using Hft.Core.RingBuffer; // NEW namespace

namespace Hft.Strategies
{
    public class MarketMakerStrategy : IStrategy
    {
        // Removed ObjectPool
        private readonly LockFreeRingBuffer<Order> _orderRing;
        private readonly MetricsCounter _ordersGenerated;
        private readonly PositionSnapshot _position;
        private readonly double _spread;
        private readonly double _qty;

        public MarketMakerStrategy(
            LockFreeRingBuffer<Order> orderRing,
            MetricsCounter ordersGenerated,
            PositionSnapshot position,
            // ObjectPool removed
            double spread = 0.02,
            double qty = 10)
        {
            _orderRing = orderRing;
            _ordersGenerated = ordersGenerated;
            _position = position;
            _spread = spread;
            _qty = qty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnTick(ref MarketDataTick tick)
        {
            double mid = (tick.BidPrice + tick.AskPrice) / 2.0;
            double myBid = mid - _spread / 2.0;
            double myAsk = mid + _spread / 2.0;

            double currentPos = _position.GetNetPosition();
            if (currentPos > 100) myBid -= 0.01;
            if (currentPos < -100) myAsk += 0.01;

            // Emit Bid
            var bidOrder = Order.Create(tick.InstrumentId, OrderSide.Buy, myBid, _qty);

            if (_orderRing.TryWrite(in bidOrder))
            {
                _ordersGenerated.Increment();
            }

            // Emit Ask
            var askOrder = Order.Create(tick.InstrumentId, OrderSide.Sell, myAsk, _qty);

            if (_orderRing.TryWrite(in askOrder))
            {
                _ordersGenerated.Increment();
            }
        }
    }
}
