using Hft.Core.RingBuffer;
using Hft.Feeds;

namespace Hft.Strategies
{
    /// <summary>
    /// Deterministic market making strategy.
    /// Places bid/ask around mid-price.
    /// </summary>
    public sealed class MarketMakerStrategy
    {
        private readonly LockFreeRingBuffer<MarketDataTick> _marketData;
        private readonly LockFreeRingBuffer<Order> _orders;

        private readonly double _spread;
        private readonly int _size;

        public MarketMakerStrategy(
            LockFreeRingBuffer<MarketDataTick> marketData,
            LockFreeRingBuffer<Order> orders,
            double spread = 0.02,
            int size = 100)
        {
            _marketData = marketData;
            _orders = orders;
            _spread = spread;
            _size = size;
        }

        public void Run()
        {
            while (true)
            {
                if (!_marketData.TryRead(out var tick))
                    continue;

                double mid = tick.Price;
                double bid = mid - _spread / 2;
                double ask = mid + _spread / 2;

                _orders.TryWrite(new Order(OrderSide.Buy, bid, _size));
                _orders.TryWrite(new Order(OrderSide.Sell, ask, _size));
            }
        }
    }
}
