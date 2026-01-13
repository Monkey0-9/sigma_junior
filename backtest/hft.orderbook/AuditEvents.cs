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
    public readonly struct AuditEvent : IEquatable<AuditEvent>
    {
        public OrderEventType EventType { get; }
        public long SequenceNumber { get; }
        public long Timestamp { get; }
        public long OrderId { get; }
        public long InstrumentId { get; }
        public long Data1 { get; }
        public long Data2 { get; }
        public long Data3 { get; }
        public long Data4 { get; }
        private readonly long _padding1;
        private readonly long _padding2;
        private readonly long _padding3;
        private readonly long _padding4;

        public AuditEvent(OrderEventType eventType, long sequenceNumber, long timestamp, long orderId, long instrumentId, long data1, long data2, long data3, long data4)
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

        public override bool Equals(object? obj) => obj is AuditEvent other && Equals(other);
        public bool Equals(AuditEvent other) => SequenceNumber == other.SequenceNumber && EventType == other.EventType;
        public override int GetHashCode() => HashCode.Combine(SequenceNumber, EventType);
        public static bool operator ==(AuditEvent left, AuditEvent right) => left.Equals(right);
        public static bool operator !=(AuditEvent left, AuditEvent right) => !left.Equals(right);


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
            OrderAttributes flags,
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
    public enum RejectReason
    {
        None = 0,
        Unknown = 1,
        InvalidOrderId = 2,
        InvalidPrice = 3,
        InvalidQuantity = 4,
        InvalidSide = 5,
        InsufficientFunds = 6,
        PositionLimitExceeded = 7,
        OrderWouldCross = 8,
        PostOnlyWouldTake = 9,
        TradingHalt = 10,
        MarketClosed = 11,
        IcebergPeakExceeded = 12,
        SelfTradePrevention = 13
    }

    /// <summary>
    /// Complete fill record for trade execution.
    /// Contains all information needed for P&L and audit.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public readonly struct FillRecord : IEquatable<FillRecord>
    {
        public long FillId { get; }
        public long SequenceNumber { get; }
        public long Timestamp { get; }
        public long AggressorOrderId { get; }
        public long PassiveOrderId { get; }
        public long InstrumentId { get; }
        public long Price { get; }
        public int Quantity { get; }
        public OrderSide Side { get; }
        public bool IsHidden { get; }
        public bool IsMidPoint { get; }
        public LiquidityType Liquidity { get; }
        private readonly long _padding1;
        private readonly long _padding2;

        public FillRecord(long fillId, long sequenceNumber, long timestamp, long aggressorOrderId, long passiveOrderId, long instrumentId, long price, int quantity, OrderSide side, bool isHidden, bool isMidPoint, LiquidityType liquidity)
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

        public override bool Equals(object? obj) => obj is FillRecord other && Equals(other);
        public bool Equals(FillRecord other) => FillId == other.FillId;
        public override int GetHashCode() => FillId.GetHashCode();
        public static bool operator ==(FillRecord left, FillRecord right) => left.Equals(right);
        public static bool operator !=(FillRecord left, FillRecord right) => !left.Equals(right);


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
        public long SignedQuantity => Side == OrderSide.Buy ? Quantity : -Quantity;

        /// <summary>
        /// Returns the notional value of this fill.
        /// </summary>
        public long Notional => Price * Quantity;
    }

    /// <summary>
    /// Liquidity type for fills.
    /// </summary>
    public enum LiquidityType
    {
        Maker = 0,
        Taker = 1
    }

    /// <summary>
    /// Order book snapshot for checkpointing and state save/restore.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public readonly struct OrderBookSnapshot : IEquatable<OrderBookSnapshot>
    {
        public int Version { get; }
        public long InstrumentId { get; }
        public long Timestamp { get; }
        public long SequenceNumber { get; }
        public long BestBidPrice { get; }
        public int BestBidSize { get; }
        public long BestAskPrice { get; }
        public int BestAskSize { get; }
        public int BidLevelCount { get; }
        public int AskLevelCount { get; }
        public int TotalOrderCount { get; }
        public long TotalBidQuantity { get; }
        public long TotalAskQuantity { get; }
        private readonly long _padding1;
        private readonly long _padding2;
        private readonly long _padding3;
        private readonly long _padding4;

        public OrderBookSnapshot(int version, long instrumentId, long timestamp, long sequenceNumber, long bestBidPrice, int bestBidSize, long bestAskPrice, int bestAskSize, int bidLevelCount, int askLevelCount, int totalOrderCount, long totalBidQuantity, long totalAskQuantity)
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

        public override bool Equals(object? obj) => obj is OrderBookSnapshot other && Equals(other);
        public bool Equals(OrderBookSnapshot other) => SequenceNumber == other.SequenceNumber && Timestamp == other.Timestamp;
        public override int GetHashCode() => HashCode.Combine(SequenceNumber, Timestamp);
        public static bool operator ==(OrderBookSnapshot left, OrderBookSnapshot right) => left.Equals(right);
        public static bool operator !=(OrderBookSnapshot left, OrderBookSnapshot right) => !left.Equals(right);


        /// <summary>
        /// Current snapshot version.
        /// </summary>
        public const int CurrentVersion = 1;
    }
}

