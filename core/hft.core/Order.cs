using System.Runtime.InteropServices;
using System.Threading;

namespace Hft.Core
{
    /// <summary>
    /// Immutable-on-creation Order struct for zero-allocation hot paths.
    /// Mutated via copy in the Strategy/Risk loop.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct Order
    {
        public long OrderId;
        public long InstrumentId;
        public OrderSide Side;
        public double Price;
        public double Quantity;
        public double LeavesQty;
        public bool IsActive;

        private static long _globalOrderIdCounter = 0;
        public static long NextId() => Interlocked.Increment(ref _globalOrderIdCounter);

        public static Order Create(long instrumentId, OrderSide side, double price, double quantity)
        {
            return new Order
            {
                OrderId = NextId(),
                InstrumentId = instrumentId,
                Side = side,
                Price = price,
                Quantity = quantity,
                LeavesQty = quantity,
                IsActive = true
            };
        }
    }
}
