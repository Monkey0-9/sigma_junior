using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hft.Core;

namespace Hft.OrderBook
{
    /// <summary>
    /// Order type enumeration for L2/L3 support.
    /// </summary>
    public enum OrderType : byte
    {
        /// <summary>Limit order - executes at specified price or better</summary>
        Limit = 0,
        
        /// <summary>Market order - executes at best available price</summary>
        Market = 1,
        
        /// <summary>Hidden order - not displayed in order book</summary>
        Hidden = 2,
        
        /// <summary>Iceberg order - displayed portion revealed gradually</summary>
        Iceberg = 3,
        
        /// <summary>Mid-point order - matches at mid-point of NBBO</summary>
        MidPoint = 4
    }

    /// <summary>
    /// Time-in-force options for orders.
    /// </summary>
    public enum TimeInForce : byte
    {
        /// <summary>Day order - expires at market close</summary>
        Day = 0,
        
        /// <summary>Good-Til-Canceled - remains active until canceled</summary>
        GTC = 1,
        
        /// <summary>Immediate-Or-Cancel - must fill immediately or cancel</summary>
        IOC = 2,
        
        /// <summary>Fill-Or-Kill - must fill completely or cancel</summary>
        FOK = 3,
        
        /// <summary>Opening - must execute during opening auction</summary>
        Opening = 4,
        
        /// <summary>Closing - must execute during closing auction</summary>
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
    public enum OrderFlags : byte
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
    public enum OrderStatus : byte
    {
        /// <summary>Order submitted but not yet in book</summary>
        Pending = 0,
        
        /// <summary>Order active in order book</summary>
        Active = 1,
        
        /// <summary>Order partially filled</summary>
        PartiallyFilled = 2,
        
        /// <summary>Order fully filled</summary>
        Filled = 3,
        
        /// <summary>Order canceled</summary>
        Canceled = 4,
        
        /// <summary>Order rejected</summary>
        Rejected = 5,
        
        /// <summary>Order expired (time-in-force)</summary>
        Expired = 6
    }

    /// <summary>
    /// Order event type for audit logging.
    /// </summary>
    public enum OrderEventType : byte
    {
        /// <summary>New order added to book</summary>
        Add = 1,
        
        /// <summary>Order canceled</summary>
        Cancel = 2,
        
        /// <summary>Order amended (quantity change)</summary>
        Amend = 3,
        
        /// <summary>Order fully or partially filled</summary>
        Fill = 4,
        
        /// <summary>Order rejected</summary>
        Reject = 5,
        
        /// <summary>Order expired</summary>
        Expire = 6,
        
        /// <summary>Best bid/ask changed</summary>
        BboChange = 7,
        
        /// <summary>Trade executed</summary>
        Trade = 8
    }

    /// <summary>
    /// Level 2 order book entry representing a price level.
    /// Aggregates multiple orders at the same price.
    /// 
    /// Performance: Blittable struct, 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public readonly struct OrderBookEntry
    {
        /// <summary>Price level (in ticks)</summary>
        public long Price { get; }

        /// <summary>Total quantity at this price level (visible + hidden)</summary>
        public int TotalQuantity { get; }

        /// <summary>Visible quantity at this price level</summary>
        public int VisibleQuantity { get; }

        /// <summary>Number of orders at this price level</summary>
        public int OrderCount { get; }

        /// <summary>Number of hidden orders at this price level</summary>
        public int HiddenOrderCount { get; }

        /// <summary>Price level sequence number (for version checking)</summary>
        public long SequenceNumber { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderBookEntry(
            long price,
            int totalQuantity,
            int visibleQuantity,
            int orderCount,
            int hiddenOrderCount,
            long sequenceNumber)
        {
            Price = price;
            TotalQuantity = totalQuantity;
            VisibleQuantity = visibleQuantity;
            OrderCount = orderCount;
            HiddenOrderCount = hiddenOrderCount;
            SequenceNumber = sequenceNumber;
        }
    }

    /// <summary>
    /// Level 3 order queue entry representing an individual order.
    /// Used for queue position tracking and L3 matching.
    /// 
    /// Performance: Blittable struct, 64 bytes (cache line aligned).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public readonly struct OrderQueueEntry
    {
        // Padding to align on 64-byte cache line
        private readonly long _padding1;
        private readonly long _padding2;
        private readonly long _padding3;
        private readonly long _padding4;
        private readonly long _padding5;
        private readonly long _padding6;
        private readonly long _padding7;

        /// <summary>Unique order identifier</summary>
        public long OrderId { get; }

        /// <summary>Original order identifier (for amendments)</summary>
        public long OriginalOrderId { get; }

        /// <summary>Instrument/symbol identifier</summary>
        public long InstrumentId { get; }

        /// <summary>Order side (Buy/Sell)</summary>
        public OrderSide Side { get; }

        /// <summary>Limit price (in ticks)</summary>
        public long Price { get; }

        /// <summary>Original order quantity</summary>
        public int OriginalQuantity { get; }

        /// <summary>Remaining quantity to be filled</summary>
        public int LeavesQuantity { get; }

        /// <summary>Order type</summary>
        public OrderType Type { get; }

        /// <summary>Time-in-force</summary>
        public TimeInForce TimeInForce { get; }

        /// <summary>Order flags (hidden, post-only, etc.)</summary>
        public OrderFlags Flags { get; }

        /// <summary>Current order status</summary>
        public OrderStatus Status { get; }

        /// <summary>Queue position (1 = front)</summary>
        public int QueuePosition { get; }

        /// <summary>Arrival timestamp (microseconds since epoch)</summary>
        public long ArrivalTimestamp { get; }

        /// <summary>Exchange-specific order reference</summary>
        public long ExchangeRef { get; }

        // Additional padding
        private readonly long _padding8;
        private readonly long _padding9;
        private readonly long _padding10;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderQueueEntry(
            long orderId,
            long originalOrderId,
            long instrumentId,
            OrderSide side,
            long price,
            int originalQuantity,
            int leavesQuantity,
            OrderType type,
            TimeInForce timeInForce,
            OrderFlags flags,
            OrderStatus status,
            int queuePosition,
            long arrivalTimestamp,
            long exchangeRef)
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

            _padding1 = _padding2 = _padding3 = _padding4 = 
                _padding5 = _padding6 = _padding7 = 
                _padding8 = _padding9 = _padding10 = 0;
        }

        /// <summary>
        /// Creates an active order queue entry.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OrderQueueEntry CreateActive(
            long orderId,
            long instrumentId,
            OrderSide side,
            long price,
            int quantity,
            OrderType type,
            TimeInForce timeInForce,
            OrderFlags flags,
            long arrivalTimestamp)
        {
            return new OrderQueueEntry(
                orderId: orderId,
                originalOrderId: orderId,
                instrumentId: instrumentId,
                side: side,
                price: price,
                originalQuantity: quantity,
                leavesQuantity: quantity,
                type: type,
                timeInForce: timeInForce,
                flags: flags,
                status: OrderStatus.Active,
                queuePosition: 0, // Set by book when added
                arrivalTimestamp: arrivalTimestamp,
                exchangeRef: 0
            );
        }

        /// <summary>
        /// Returns true if this order is hidden (not displayed).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsHidden => (Flags & OrderFlags.Hidden) != 0;

        /// <summary>
        /// Returns true if this is a post-only order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsPostOnly => (Flags & OrderFlags.PostOnly) != 0;

        /// <summary>
        /// Returns true if this is a reduce-only order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsReduceOnly => (Flags & OrderFlags.ReduceOnly) != 0;

        /// <summary>
        /// Returns true if this order can still be filled.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsActive => Status == OrderStatus.Active || Status == OrderStatus.PartiallyFilled;

        /// <summary>
        /// Returns the display quantity (visible portion).
        /// For iceberg orders, this would be the visible peak.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetDisplayQuantity()
        {
            if (IsHidden)
                return 0;
            return LeavesQuantity;
        }

        /// <summary>
        /// Creates a copy with updated leaves quantity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderQueueEntry WithLeavesQuantity(int newLeavesQty)
        {
            return new OrderQueueEntry(
                OrderId,
                OriginalOrderId,
                InstrumentId,
                Side,
                Price,
                OriginalQuantity,
                newLeavesQty,
                Type,
                TimeInForce,
                Flags,
                newLeavesQty == 0 ? OrderStatus.Filled : OrderStatus.PartiallyFilled,
                QueuePosition,
                ArrivalTimestamp,
                ExchangeRef
            );
        }

        /// <summary>
        /// Creates a copy with updated status.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderQueueEntry WithStatus(OrderStatus newStatus)
        {
            return new OrderQueueEntry(
                OrderId,
                OriginalOrderId,
                InstrumentId,
                Side,
                Price,
                OriginalQuantity,
                LeavesQuantity,
                Type,
                TimeInForce,
                Flags,
                newStatus,
                QueuePosition,
                ArrivalTimestamp,
                ExchangeRef
            );
        }

        /// <summary>
        /// Creates a copy with updated queue position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderQueueEntry WithQueuePosition(int newPosition)
        {
            return new OrderQueueEntry(
                OrderId,
                OriginalOrderId,
                InstrumentId,
                Side,
                Price,
                OriginalQuantity,
                LeavesQuantity,
                Type,
                TimeInForce,
                Flags,
                Status,
                newPosition,
                ArrivalTimestamp,
                ExchangeRef
            );
        }
    }

    /// <summary>
    /// Best bid/ask snapshot for market data.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public readonly struct BestBidAsk
    {
        public long BestBidPrice { get; }
        public int BestBidSize { get; }
        public long BestAskPrice { get; }
        public int BestAskSize { get; }
        public long Timestamp { get; }
        public long SequenceNumber { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BestBidAsk(
            long bestBidPrice,
            int bestBidSize,
            long bestAskPrice,
            int bestAskSize,
            long timestamp,
            long sequenceNumber)
        {
            BestBidPrice = bestBidPrice;
            BestBidSize = bestBidSize;
            BestAskPrice = bestAskPrice;
            BestAskSize = bestAskSize;
            Timestamp = timestamp;
            SequenceNumber = sequenceNumber;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasBothSides => BestBidPrice > 0 && BestAskPrice > 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long MidPrice => (BestBidPrice + BestAskPrice) / 2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Spread => BestAskPrice - BestBidPrice;
    }

    /// <summary>
    /// Order book depth at a given price level.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public readonly struct BookDepth
    {
        public long Price { get; }
        public int BidDepth { get; }
        public int AskDepth { get; }
        public int BidOrders { get; }
        public int AskOrders { get; }
        public int BidHidden { get; }
        public int AskHidden { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BookDepth(
            long price,
            int bidDepth,
            int askDepth,
            int bidOrders,
            int askOrders,
            int bidHidden,
            int askHidden)
        {
            Price = price;
            BidDepth = bidDepth;
            AskDepth = askDepth;
            BidOrders = bidOrders;
            AskOrders = askOrders;
            BidHidden = bidHidden;
            AskHidden = askHidden;
        }

        /// <summary>
        /// Total visible liquidity at this price (bid + ask).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int TotalVisibleLiquidity => BidDepth + AskDepth;

        /// <summary>
        /// Total hidden liquidity at this price.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int TotalHiddenLiquidity => BidHidden + AskHidden;
    }
}

