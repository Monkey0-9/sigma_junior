using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hft.Core;

namespace Hft.OrderBook
{
    /// <summary>
    /// Order type enumeration for L2/L3 support.
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
    /// Bit 0: Hidden (not displayed)
    /// Bit 1: Post-only (must provide liquidity)
    /// Bit 2: Reduce-only (can only reduce position)
    /// Bit 3: Auto-create (creates quote in auction)
    /// Bits 4-7: Reserved for future use
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
    /// Order event type for audit logging.
    /// </summary>
    public enum OrderEventType
    {
        None = 0,
        Add = 1,
        Cancel = 2,
        Amend = 3,
        Fill = 4,
        Reject = 5,
        Expire = 6,
        BboChange = 7,
        Trade = 8
    }

    /// <summary>
    /// Level 2 order book entry representing a price level.
    /// Aggregates multiple orders at the same price.
    /// 
    /// Performance: Blittable struct, 32 bytes.
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
    /// Used for queue position tracking and L3 matching.
    /// 
    /// Performance: Blittable struct, 64 bytes (cache line aligned).
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
}

