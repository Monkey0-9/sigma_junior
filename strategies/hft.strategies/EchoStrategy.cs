using System.Runtime.CompilerServices;
using Hft.Core;
using Hft.Core.RingBuffer;

namespace Hft.Strategies
{
    public class EchoStrategy : IStrategy
    {
        private readonly LockFreeRingBuffer<Order> _orderRing;
        private readonly MetricsCounter _ordersGenerated;

        public EchoStrategy(
            LockFreeRingBuffer<Order> orderRing,
            MetricsCounter ordersGenerated)
        {
            _orderRing = orderRing;
            _ordersGenerated = ordersGenerated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnTick(ref MarketDataTick tick)
        {
            var order = Order.Create(tick.InstrumentId, OrderSide.Buy, tick.AskPrice, 1);

            if (_orderRing.TryWrite(in order))
            {
                _ordersGenerated.Increment();
            }
        }
    }
}
