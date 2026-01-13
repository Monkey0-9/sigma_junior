using System;
using System.Runtime.InteropServices;

namespace Hft.Core
{
    /// <summary>
    /// Institutional side definition.
    /// Renamed to OrderSide for legacy compatibility.
    /// </summary>
    public enum OrderSide : int
    {
        None = 0,
        Buy = 1,
        Sell = 2
    }

    /// <summary>
    /// Price Level for L2 Order Book snapshots.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct PriceLevel : IEquatable<PriceLevel>
    {
        public readonly double Price;
        public readonly double Size;

        public PriceLevel(double p, double s)
        {
            Price = p;
            Size = s;
        }

        public readonly bool Equals(PriceLevel other) =>
            Price == other.Price && Size == other.Size;

        public readonly override bool Equals(object? obj) =>
            obj is PriceLevel other && Equals(other);

        public readonly override int GetHashCode() =>
            HashCode.Combine(Price, Size);

        public static bool operator ==(PriceLevel left, PriceLevel right) =>
            left.Equals(right);

        public static bool operator !=(PriceLevel left, PriceLevel right) =>
            !left.Equals(right);
    }

    /// <summary>
    /// Institutional Order Primitive.
    /// Immutable value type for zero-allocation strategy paths.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct Order : IEquatable<Order>
    {
        public readonly byte Version;      // Schema version
        public readonly long OrderId;
        public readonly long InstrumentId;
        public readonly OrderSide Side;
        public readonly double Price;
        public readonly double Quantity;
        public readonly long TimestampTicks;
        public readonly long Sequence;

        public Order(long orderId, long instrumentId, OrderSide side, double price, double quantity, long ts, long seq)
        {
            Version = 1;
            OrderId = orderId;
            InstrumentId = instrumentId;
            Side = side;
            Price = price;
            Quantity = quantity;
            TimestampTicks = ts;
            Sequence = seq;
        }

        // Functional WithX helpers to maintain immutability
        public Order WithPrice(double price) => new Order(OrderId, InstrumentId, Side, price, Quantity, TimestampTicks, Sequence);
        public Order WithQuantity(double qty) => new Order(OrderId, InstrumentId, Side, Price, qty, TimestampTicks, Sequence);

        public readonly bool Equals(Order other) =>
            OrderId == other.OrderId && InstrumentId == other.InstrumentId &&
            Side == other.Side && Price == other.Price && Quantity == other.Quantity &&
            TimestampTicks == other.TimestampTicks && Sequence == other.Sequence;

        public readonly override bool Equals(object? obj) =>
            obj is Order other && Equals(other);

        public readonly override int GetHashCode() =>
            HashCode.Combine(OrderId, InstrumentId, Side, Price, Quantity, TimestampTicks, Sequence);

        public static bool operator ==(Order left, Order right) =>
            left.Equals(right);

        public static bool operator !=(Order left, Order right) =>
            !left.Equals(right);
    }

    /// <summary>
    /// Institutional Fill Primitive.
    /// Immutable record of execution event.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct Fill : IEquatable<Fill>
    {
        public readonly byte Version;
        public readonly long FillId;
        public readonly long OrderId;
        public readonly long InstrumentId;
        public readonly OrderSide Side;
        public readonly double Price;
        public readonly double Quantity;
        public readonly long TimestampTicks;

        public Fill(long fillId, long orderId, long instrumentId, OrderSide side, double price, double quantity, long ts)
        {
            Version = 1;
            FillId = fillId;
            OrderId = orderId;
            InstrumentId = instrumentId;
            Side = side;
            Price = price;
            Quantity = quantity;
            TimestampTicks = ts;
        }

        public static Fill CreateWithId(long fillId, long orderId, long instrumentId, OrderSide side, double price, double quantity, long ts)
            => new Fill(fillId, orderId, instrumentId, side, price, quantity, ts);

        public readonly bool Equals(Fill other) =>
            FillId == other.FillId && OrderId == other.OrderId &&
            InstrumentId == other.InstrumentId && Side == other.Side &&
            Price == other.Price && Quantity == other.Quantity &&
            TimestampTicks == other.TimestampTicks;

        public readonly override bool Equals(object? obj) =>
            obj is Fill other && Equals(other);

        public readonly override int GetHashCode() =>
            HashCode.Combine(FillId, OrderId, InstrumentId, Side, Price, Quantity, TimestampTicks);

        public static bool operator ==(Fill left, Fill right) =>
            left.Equals(right);

        public static bool operator !=(Fill left, Fill right) =>
            !left.Equals(right);
    }

    /// <summary>
    /// Institutional Level 2 Market Data.
    /// Explicitly laid out for MemoryMarshal / Span optimization.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct MarketDataTick : IEquatable<MarketDataTick>
    {
        public readonly byte Version;
        public readonly long Sequence;
        public readonly long InstrumentId;
        public readonly long SendTimestampTicks;
        public readonly long ReceiveTimestampTicks;

        // L2 Depth - Fixed length for stability and serialization predictability
        public readonly PriceLevel Bid1; public readonly PriceLevel Bid2; public readonly PriceLevel Bid3; public readonly PriceLevel Bid4; public readonly PriceLevel Bid5;
        public readonly PriceLevel Ask1; public readonly PriceLevel Ask2; public readonly PriceLevel Ask3; public readonly PriceLevel Ask4; public readonly PriceLevel Ask5;

        public MarketDataTick(long seq, long instId, long sendTs, long recvTs, PriceLevel[] bids, PriceLevel[] asks)
        {
            ArgumentNullException.ThrowIfNull(bids);
            ArgumentNullException.ThrowIfNull(asks);

            Version = 1;
            Sequence = seq;
            InstrumentId = instId;
            SendTimestampTicks = sendTs;
            ReceiveTimestampTicks = recvTs;

            Bid1 = bids.Length > 0 ? bids[0] : default; Bid2 = bids.Length > 1 ? bids[1] : default; Bid3 = bids.Length > 2 ? bids[2] : default; Bid4 = bids.Length > 3 ? bids[3] : default; Bid5 = bids.Length > 4 ? bids[4] : default;
            Ask1 = asks.Length > 0 ? asks[0] : default; Ask2 = asks.Length > 1 ? asks[1] : default; Ask3 = asks.Length > 2 ? asks[2] : default; Ask4 = asks.Length > 3 ? asks[3] : default; Ask5 = asks.Length > 4 ? asks[4] : default;
        }

        public double BestBid => Bid1.Price;
        public double BestAsk => Ask1.Price;
        public double MidPrice => (BestBid + BestAsk) / 2.0;

        public readonly bool Equals(MarketDataTick other) =>
            Sequence == other.Sequence && InstrumentId == other.InstrumentId &&
            SendTimestampTicks == other.SendTimestampTicks && ReceiveTimestampTicks == other.ReceiveTimestampTicks &&
            Bid1.Equals(other.Bid1) && Bid2.Equals(other.Bid2) && Bid3.Equals(other.Bid3) && Bid4.Equals(other.Bid4) && Bid5.Equals(other.Bid5) &&
            Ask1.Equals(other.Ask1) && Ask2.Equals(other.Ask2) && Ask3.Equals(other.Ask3) && Ask4.Equals(other.Ask4) && Ask5.Equals(other.Ask5);

        public readonly override bool Equals(object? obj) =>
            obj is MarketDataTick other && Equals(other);

        public readonly override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Sequence);
            hash.Add(InstrumentId);
            hash.Add(SendTimestampTicks);
            hash.Add(ReceiveTimestampTicks);
            hash.Add(Bid1);
            hash.Add(Bid2);
            hash.Add(Bid3);
            hash.Add(Bid4);
            hash.Add(Bid5);
            hash.Add(Ask1);
            hash.Add(Ask2);
            hash.Add(Ask3);
            hash.Add(Ask4);
            hash.Add(Ask5);
            return hash.ToHashCode();
        }

        public static bool operator ==(MarketDataTick left, MarketDataTick right) =>
            left.Equals(right);

        public static bool operator !=(MarketDataTick left, MarketDataTick right) =>
            !left.Equals(right);
    }
}

