using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Hft.OrderBook
{
    /// <summary>
    /// Base audit event structure for all order book events.
    /// Every event is logged for deterministic replay and audit.
    /// 
    /// Performance: Blittable struct, 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public readonly struct AuditEvent
    {
        /// <summary>Event type (1 byte + 3 padding)</summary>
        public OrderEventType EventType { get; }

        /// <summary>Event sequence number (monotonically increasing)</summary>
        public long SequenceNumber { get; }

        /// <summary>Event timestamp (microseconds since epoch)</summary>
        public long Timestamp { get; }

        /// <summary>Order ID involved (0 if not applicable)</summary>
        public long OrderId { get; }

        /// <summary>Instrument/symbol ID</summary>
        public long InstrumentId { get; }

        /// <summary>Event-specific data (packed)</summary>
        public long Data1 { get; }

        /// <summary>Event-specific data (packed)</summary>
        public long Data2 { get; }

        /// <summary>Event-specific data (packed)</summary>
        public long Data3 { get; }

        /// <summary>Event-specific data (packed)</summary>
        public long Data4 { get; }

        // Padding to 32 bytes
        private readonly long _padding1;
        private readonly long _padding2;
        private readonly long _padding3;
        private readonly long _padding4;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AuditEvent(
            OrderEventType eventType,
            long sequenceNumber,
            long timestamp,
            long orderId,
            long instrumentId,
            long data1,
            long data2,
            long data3,
            long data4)
        {
            EventType = eventType;
            SequenceNumber = sequenceNumber;
            Timestamp = timestamp;
            OrderId = orderId;
            InstrumentId = instrumentId;
            Data1 = data1;
            Data2 = data2;
            Data3 = data3;
            Data4 = data4;
            _padding1 = _padding2 = _padding3 = _padding4 = 0;
        }

        /// <summary>
        /// Creates an Order Add event.
        /// Data1: Price, Data2: Quantity, Data3: Side+Type+Flags (packed), Data4: QueuePosition
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AuditEvent CreateAddEvent(
            long sequenceNumber,
            long timestamp,
            long orderId,
            long instrumentId,
            long price,
            int quantity,
            OrderSide side,
            OrderType type,
            OrderFlags flags,
            int queuePosition)
        {
            // Pack side, type, flags into Data3 (8 bits each: side=1, type=4, flags=3)
            long packedAttributes = ((long)side << 16) | ((long)type << 8) | (long)flags;
            
            return new AuditEvent(
                eventType: OrderEventType.Add,
                sequenceNumber: sequenceNumber,
                timestamp: timestamp,
                orderId: orderId,
                instrumentId: instrumentId,
                data1: price,
                data2: quantity,
                data3: packedAttributes,
                data4: queuePosition
            );
        }

        /// <summary>
        /// Creates an Order Cancel event.
        /// Data1: Remaining quantity (0 if full cancel), Data2: Original quantity
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AuditEvent CreateCancelEvent(
            long sequenceNumber,
            long timestamp,
            long orderId,
            long instrumentId,
            int leavesQuantity,
            int originalQuantity)
        {
            return new AuditEvent(
                eventType: OrderEventType.Cancel,
                sequenceNumber: sequenceNumber,
                timestamp: timestamp,
                orderId: orderId,
                instrumentId: instrumentId,
                data1: leavesQuantity,
                data2: originalQuantity,
                data3: 0,
                data4: 0
            );
        }

        /// <summary>
        /// Creates an Order Amend event.
        /// Data1: New quantity, Data2: Old quantity, Data3: New price, Data4: Old price
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AuditEvent CreateAmendEvent(
            long sequenceNumber,
            long timestamp,
            long orderId,
            long instrumentId,
            int newQuantity,
            int oldQuantity,
            long newPrice,
            long oldPrice)
        {
            return new AuditEvent(
                eventType: OrderEventType.Amend,
                sequenceNumber: sequenceNumber,
                timestamp: timestamp,
                orderId: orderId,
                instrumentId: instrumentId,
                data1: newQuantity,
                data2: oldQuantity,
                data3: newPrice,
                data4: oldPrice
            );
        }

        /// <summary>
        /// Creates a Fill event.
        /// Data1: Fill price, Data2: Fill quantity, Data3: Total filled quantity, Data4: Counterparty order ID
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AuditEvent CreateFillEvent(
            long sequenceNumber,
            long timestamp,
            long orderId,
            long instrumentId,
            long fillPrice,
            int fillQuantity,
            int totalFilledQuantity,
            long counterpartyOrderId)
        {
            return new AuditEvent(
                eventType: OrderEventType.Fill,
                sequenceNumber: sequenceNumber,
                timestamp: timestamp,
                orderId: orderId,
                instrumentId: instrumentId,
                data1: fillPrice,
                data2: fillQuantity,
                data3: totalFilledQuantity,
                data4: counterpartyOrderId
            );
        }

        /// <summary>
        /// Creates a Trade event (for public trade log).
        /// Data1: Trade price, Data2: Trade quantity, Data3: Buy order ID, Data4: Sell order ID
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AuditEvent CreateTradeEvent(
            long sequenceNumber,
            long timestamp,
            long instrumentId,
            long tradePrice,
            int tradeQuantity,
            long buyOrderId,
            long sellOrderId)
        {
            return new AuditEvent(
                eventType: OrderEventType.Trade,
                sequenceNumber: sequenceNumber,
                timestamp: timestamp,
                orderId: 0, // Trade event has no single order
                instrumentId: instrumentId,
                data1: tradePrice,
                data2: tradeQuantity,
                data3: buyOrderId,
                data4: sellOrderId
            );
        }

        /// <summary>
        /// Creates a BBO Change event.
        /// Data1: New bid price, Data2: New bid size, Data3: New ask price, Data4: New ask size
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AuditEvent CreateBboChangeEvent(
            long sequenceNumber,
            long timestamp,
            long instrumentId,
            long bidPrice,
            int bidSize,
            long askPrice,
            int askSize)
        {
            return new AuditEvent(
                eventType: OrderEventType.BboChange,
                sequenceNumber: sequenceNumber,
                timestamp: timestamp,
                orderId: 0,
                instrumentId: instrumentId,
                data1: bidPrice,
                data2: bidSize,
                data3: askPrice,
                data4: askSize
            );
        }

        /// <summary>
        /// Creates a Reject event.
        /// Data1: Reject reason code, Data2: Original order quantity, Data3: Original price
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AuditEvent CreateRejectEvent(
            long sequenceNumber,
            long timestamp,
            long orderId,
            long instrumentId,
            RejectReason reason,
            int originalQuantity,
            long originalPrice)
        {
            return new AuditEvent(
                eventType: OrderEventType.Reject,
                sequenceNumber: sequenceNumber,
                timestamp: timestamp,
                orderId: orderId,
                instrumentId: instrumentId,
                data1: (long)reason,
                data2: originalQuantity,
                data3: originalPrice,
                data4: 0
            );
        }

        /// <summary>
        /// Returns a string representation of this event.
        /// </summary>
        public override string ToString()
        {
            return $"Event[{SequenceNumber}] {EventType} @ {Timestamp}μs OrderId={OrderId} " +
                   $"Data1={Data1} Data2={Data2} Data3={Data3} Data4={Data4}";
        }
    }

    /// <summary>
    /// Reject reason codes for order rejections.
    /// </summary>
    public enum RejectReason : byte
    {
        Unknown = 0,
        InvalidOrderId = 1,
        InvalidPrice = 2,
        InvalidQuantity = 3,
        InvalidSide = 4,
        InsufficientFunds = 5,
        PositionLimitExceeded = 6,
        OrderWouldCross = 7,
        PostOnlyWouldTake = 8,
        TradingHalt = 9,
        MarketClosed = 10,
        IcebergPeakExceeded = 11,
        SelfTradePrevention = 12
    }

    /// <summary>
    /// Complete fill record for trade execution.
    /// Contains all information needed for P&L and audit.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public readonly struct FillRecord
    {
        /// <summary>Unique fill ID</summary>
        public long FillId { get; }

        /// <summary>Sequence number of the fill event</summary>
        public long SequenceNumber { get; }

        /// <summary>Timestamp of the fill (microseconds)</summary>
        public long Timestamp { get; }

        /// <summary>Aggressive order ID</summary>
        public long AggressorOrderId { get; }

        /// <summary>Passive order ID (the order that was filled)</summary>
        public long PassiveOrderId { get; }

        /// <summary>Instrument ID</summary>
        public long InstrumentId { get; }

        /// <summary>Fill price</summary>
        public long Price { get; }

        /// <summary>Fill quantity</summary>
        public int Quantity { get; }

        /// <summary>Aggressive order side</summary>
        public OrderSide Side { get; }

        /// <summary>Was this a hidden order fill?</summary>
        public bool IsHidden { get; }

        /// <summary>Was this a mid-point match?</summary>
        public bool IsMidPoint { get; }

        /// <summary>Liability (liquidity providing vs taking)</summary>
        public LiquidityType Liquidity { get; }

        // Padding
        private readonly long _padding1;
        private readonly long _padding2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FillRecord(
            long fillId,
            long sequenceNumber,
            long timestamp,
            long aggressorOrderId,
            long passiveOrderId,
            long instrumentId,
            long price,
            int quantity,
            OrderSide side,
            bool isHidden,
            bool isMidPoint,
            LiquidityType liquidity)
        {
            FillId = fillId;
            SequenceNumber = sequenceNumber;
            Timestamp = timestamp;
            AggressorOrderId = aggressorOrderId;
            PassiveOrderId = passiveOrderId;
            InstrumentId = instrumentId;
            Price = price;
            Quantity = quantity;
            Side = side;
            IsHidden = isHidden;
            IsMidPoint = isMidPoint;
            Liquidity = liquidity;
            _padding1 = _padding2 = 0;
        }

        /// <summary>
        /// Creates a fill record from an audit event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FillRecord FromAuditEvent(
            AuditEvent fillEvent,
            long aggressorOrderId,
            LiquidityType liquidity)
        {
            return new FillRecord(
                fillId: fillEvent.SequenceNumber, // Use seq num as fill ID
                sequenceNumber: fillEvent.SequenceNumber,
                timestamp: fillEvent.Timestamp,
                aggressorOrderId: aggressorOrderId,
                passiveOrderId: fillEvent.OrderId,
                instrumentId: fillEvent.InstrumentId,
                price: fillEvent.Data1,
                quantity: (int)fillEvent.Data2,
                side: (OrderSide)((fillEvent.Data3 >> 16) & 0xFF),
                isHidden: (fillEvent.Data3 & 0xFF) != 0,
                isMidPoint: false, // Set by matching engine
                liquidity: liquidity
            );
        }

        /// <summary>
        /// Returns the signed quantity (positive for buy, negative for sell).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long SignedQuantity => Side == OrderSide.Buy ? Quantity : -Quantity;

        /// <summary>
        /// Returns the notional value of this fill.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Notional => Price * Quantity;
    }

    /// <summary>
    /// Liquidity type for fills.
    /// </summary>
    public enum LiquidityType : byte
    {
        /// <summary>Maker (provided liquidity, passive order)</summary>
        Maker = 0,
        
        /// <summary>Taker (removed liquidity, aggressive order)</summary>
        Taker = 1
    }

    /// <summary>
    /// Order book snapshot for checkpointing and state save/restore.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public readonly struct OrderBookSnapshot
    {
        /// <summary>Snapshot version (for format compatibility)</summary>
        public int Version { get; }

        /// <summary>Instrument ID</summary>
        public long InstrumentId { get; }

        /// <summary>Snapshot timestamp</summary>
        public long Timestamp { get; }

        /// <summary>Sequence number at snapshot</summary>
        public long SequenceNumber { get; }

        /// <summary>Best bid price</summary>
        public long BestBidPrice { get; }

        /// <summary>Best bid size</summary>
        public int BestBidSize { get; }

        /// <summary>Best ask price</summary>
        public long BestAskPrice { get; }

        /// <summary>Best ask size</summary>
        public int BestAskSize { get; }

        /// <summary>Number of price levels on bid side</summary>
        public int BidLevelCount { get; }

        /// <summary>Number of price levels on ask side</summary>
        public int AskLevelCount { get; }

        /// <summary>Total number of active orders</summary>
        public int TotalOrderCount { get; }

        /// <summary>Total buy-side quantity</summary>
        public long TotalBidQuantity { get; }

        /// <summary>Total sell-side quantity</summary>
        public long TotalAskQuantity { get; }

        // Padding
        private readonly long _padding1;
        private readonly long _padding2;
        private readonly long _padding3;
        private readonly long _padding4;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderBookSnapshot(
            int version,
            long instrumentId,
            long timestamp,
            long sequenceNumber,
            long bestBidPrice,
            int bestBidSize,
            long bestAskPrice,
            int bestAskSize,
            int bidLevelCount,
            int askLevelCount,
            int totalOrderCount,
            long totalBidQuantity,
            long totalAskQuantity)
        {
            Version = version;
            InstrumentId = instrumentId;
            Timestamp = timestamp;
            SequenceNumber = sequenceNumber;
            BestBidPrice = bestBidPrice;
            BestBidSize = bestBidSize;
            BestAskPrice = bestAskPrice;
            BestAskSize = bestAskSize;
            BidLevelCount = bidLevelCount;
            AskLevelCount = askLevelCount;
            TotalOrderCount = totalOrderCount;
            TotalBidQuantity = totalBidQuantity;
            TotalAskQuantity = totalAskQuantity;
            _padding1 = _padding2 = _padding3 = _padding4 = 0;
        }

        /// <summary>
        /// Current snapshot version.
        /// </summary>
        public const int CurrentVersion = 1;
    }
}

