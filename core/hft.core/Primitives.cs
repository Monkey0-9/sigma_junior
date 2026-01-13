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
    /// Order type enumeration.
    /// Moved from OrderBook for architectural layering.
    /// </summary>
    public enum OrderType
    {
        Limit = 0,
        Market = 1,
        Hidden = 2,
        Iceberg = 3,
        MidPoint = 4
    }

    /// <summary>
    /// Time-in-force options for orders.
    /// </summary>
    public enum TimeInForce
    {
        Day = 0,
        GTC = 1,
        IOC = 2,
        FOK = 3,
        Opening = 4,
        Closing = 5
    }

    /// <summary>
    /// Order flags for additional order behavior.
    /// </summary>
    [Flags]
    public enum OrderAttributes
    {
        None = 0,
        Hidden = 1 << 0,
        PostOnly = 1 << 1,
        ReduceOnly = 1 << 2,
        AutoCreate = 1 << 3
    }

    /// <summary>
    /// Order status enumeration.
    /// </summary>
    public enum OrderStatus
    {
        Pending = 0,
        Active = 1,
        PartiallyFilled = 2,
        Filled = 3,
        Canceled = 4,
        Rejected = 5,
        Expired = 6
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
    /// Level 2 order book entry representing a price level.
    /// Moved from OrderBook types for sharing.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public readonly struct OrderBookEntry : IEquatable<OrderBookEntry>
    {
        public long Price { get; }
        public int TotalQuantity { get; }
        public int VisibleQuantity { get; }
        public int OrderCount { get; }
        public int HiddenOrderCount { get; }
        public long SequenceNumber { get; }

        public OrderBookEntry(long price, int totalQuantity, int visibleQuantity, int orderCount, int hiddenOrderCount, long sequenceNumber)
        {
            Price = price;
            TotalQuantity = totalQuantity;
            VisibleQuantity = visibleQuantity;
            OrderCount = orderCount;
            HiddenOrderCount = hiddenOrderCount;
            SequenceNumber = sequenceNumber;
        }

        public override bool Equals(object? obj) => obj is OrderBookEntry other && Equals(other);
        public bool Equals(OrderBookEntry other) => Price == other.Price && TotalQuantity == other.TotalQuantity && VisibleQuantity == other.VisibleQuantity && OrderCount == other.OrderCount && HiddenOrderCount == other.HiddenOrderCount && SequenceNumber == other.SequenceNumber;
        public override int GetHashCode() => HashCode.Combine(Price, TotalQuantity, VisibleQuantity, OrderCount, HiddenOrderCount, SequenceNumber);
        public static bool operator ==(OrderBookEntry left, OrderBookEntry right) => left.Equals(right);
        public static bool operator !=(OrderBookEntry left, OrderBookEntry right) => !left.Equals(right);
    }

    /// <summary>
    /// Level 3 order queue entry representing an individual order.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public readonly struct OrderQueueEntry : IEquatable<OrderQueueEntry>
    {
        private readonly long _padding1;
        private readonly long _padding2;
        private readonly long _padding3;
        private readonly long _padding4;
        private readonly long _padding5;
        private readonly long _padding6;
        private readonly long _padding7;

        public long OrderId { get; }
        public long OriginalOrderId { get; }
        public long InstrumentId { get; }
        public OrderSide Side { get; }
        public long Price { get; }
        public int OriginalQuantity { get; }
        public int LeavesQuantity { get; }
        public OrderType Type { get; }
        public TimeInForce TimeInForce { get; }
        public OrderAttributes Flags { get; }
        public OrderStatus Status { get; }
        public int QueuePosition { get; }
        public long ArrivalTimestamp { get; }
        public long ExchangeRef { get; }

        private readonly long _padding8;
        private readonly long _padding9;
        private readonly long _padding10;

        public OrderQueueEntry(long orderId, long originalOrderId, long instrumentId, OrderSide side, long price, int originalQuantity, int leavesQuantity, OrderType type, TimeInForce timeInForce, OrderAttributes flags, OrderStatus status, int queuePosition, long arrivalTimestamp, long exchangeRef)
        {
            OrderId = orderId;
            OriginalOrderId = originalOrderId;
            InstrumentId = instrumentId;
            Side = side;
            Price = price;
            OriginalQuantity = originalQuantity;
            LeavesQuantity = leavesQuantity;
            Type = type;
            TimeInForce = timeInForce;
            Flags = flags;
            Status = status;
            QueuePosition = queuePosition;
            ArrivalTimestamp = arrivalTimestamp;
            ExchangeRef = exchangeRef;
            _padding1 = _padding2 = _padding3 = _padding4 = _padding5 = _padding6 = _padding7 = _padding8 = _padding9 = _padding10 = 0;
        }

        public static OrderQueueEntry CreateActive(long orderId, long instrumentId, OrderSide side, long price, int quantity, OrderType type, TimeInForce timeInForce, OrderAttributes flags, long arrivalTimestamp)
        {
            return new OrderQueueEntry(orderId, orderId, instrumentId, side, price, quantity, quantity, type, timeInForce, flags, OrderStatus.Active, 0, arrivalTimestamp, 0);
        }

        public bool IsHidden => (Flags & OrderAttributes.Hidden) != 0;
        public bool IsPostOnly => (Flags & OrderAttributes.PostOnly) != 0;
        public bool IsReduceOnly => (Flags & OrderAttributes.ReduceOnly) != 0;
        public bool IsActive => Status == OrderStatus.Active || Status == OrderStatus.PartiallyFilled;
        public int DisplayQuantity => IsHidden ? 0 : LeavesQuantity;

        public OrderQueueEntry WithLeavesQuantity(int newLeavesQty) => new OrderQueueEntry(OrderId, OriginalOrderId, InstrumentId, Side, Price, OriginalQuantity, newLeavesQty, Type, TimeInForce, Flags, newLeavesQty == 0 ? OrderStatus.Filled : OrderStatus.PartiallyFilled, QueuePosition, ArrivalTimestamp, ExchangeRef);
        public OrderQueueEntry WithStatus(OrderStatus newStatus) => new OrderQueueEntry(OrderId, OriginalOrderId, InstrumentId, Side, Price, OriginalQuantity, LeavesQuantity, Type, TimeInForce, Flags, newStatus, QueuePosition, ArrivalTimestamp, ExchangeRef);
        public OrderQueueEntry WithQueuePosition(int newPosition) => new OrderQueueEntry(OrderId, OriginalOrderId, InstrumentId, Side, Price, OriginalQuantity, LeavesQuantity, Type, TimeInForce, Flags, Status, newPosition, ArrivalTimestamp, ExchangeRef);

        public override bool Equals(object? obj) => obj is OrderQueueEntry other && Equals(other);
        public bool Equals(OrderQueueEntry other) => OrderId == other.OrderId && LeavesQuantity == other.LeavesQuantity && Status == other.Status;
        public override int GetHashCode() => HashCode.Combine(OrderId, LeavesQuantity, Status);
        public static bool operator ==(OrderQueueEntry left, OrderQueueEntry right) => left.Equals(right);
        public static bool operator !=(OrderQueueEntry left, OrderQueueEntry right) => !left.Equals(right);
    }

    /// <summary>
    /// Best bid/ask snapshot for market data.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public readonly struct BestBidAsk : IEquatable<BestBidAsk>
    {
        public long BestBidPrice { get; }
        public int BestBidSize { get; }
        public long BestAskPrice { get; }
        public int BestAskSize { get; }
        public long Timestamp { get; }
        public long SequenceNumber { get; }

        public BestBidAsk(long bestBidPrice, int bestBidSize, long bestAskPrice, int bestAskSize, long timestamp, long sequenceNumber)
        {
            BestBidPrice = bestBidPrice;
            BestBidSize = bestBidSize;
            BestAskPrice = bestAskPrice;
            BestAskSize = bestAskSize;
            Timestamp = timestamp;
            SequenceNumber = sequenceNumber;
        }

        public bool HasBothSides => BestBidPrice > 0 && BestAskPrice > 0;
        public long MidPrice => (BestBidPrice + BestAskPrice) / 2;
        public long Spread => BestAskPrice - BestBidPrice;

        public override bool Equals(object? obj) => obj is BestBidAsk other && Equals(other);
        public bool Equals(BestBidAsk other) => BestBidPrice == other.BestBidPrice && BestBidSize == other.BestBidSize && BestAskPrice == other.BestAskPrice && BestAskSize == other.BestAskSize && Timestamp == other.Timestamp && SequenceNumber == other.SequenceNumber;
        public override int GetHashCode() => HashCode.Combine(BestBidPrice, BestBidSize, BestAskPrice, BestAskSize, Timestamp, SequenceNumber);
        public static bool operator ==(BestBidAsk left, BestBidAsk right) => left.Equals(right);
        public static bool operator !=(BestBidAsk left, BestBidAsk right) => !left.Equals(right);
    }

    /// <summary>
    /// Order book depth at a given price level.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public readonly struct BookDepth : IEquatable<BookDepth>
    {
        public long Price { get; }
        public int BidDepth { get; }
        public int AskDepth { get; }
        public int BidOrders { get; }
        public int AskOrders { get; }
        public int BidHidden { get; }
        public int AskHidden { get; }

        public BookDepth(long price, int bidDepth, int askDepth, int bidOrders, int askOrders, int bidHidden, int askHidden)
        {
            Price = price;
            BidDepth = bidDepth;
            AskDepth = askDepth;
            BidOrders = bidOrders;
            AskOrders = askOrders;
            BidHidden = bidHidden;
            AskHidden = askHidden;
        }

        public int TotalVisibleLiquidity => BidDepth + AskDepth;
        public int TotalHiddenLiquidity => BidHidden + AskHidden;

        public override bool Equals(object? obj) => obj is BookDepth other && Equals(other);
        public bool Equals(BookDepth other) => Price == other.Price && BidDepth == other.BidDepth && AskDepth == other.AskDepth && BidOrders == other.BidOrders && AskOrders == other.AskOrders && BidHidden == other.BidHidden && AskHidden == other.AskHidden;
        public override int GetHashCode() => HashCode.Combine(Price, BidDepth, AskDepth, BidOrders, AskOrders, BidHidden, AskHidden);
        public static bool operator ==(BookDepth left, BookDepth right) => left.Equals(right);
        public static bool operator !=(BookDepth left, BookDepth right) => !left.Equals(right);
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
