using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Hft.OrderBook
{
    /// <summary>
    /// Main interface for the order book simulator.
    /// Provides synchronous API for market data injection and order submission.
    /// </summary>
    public interface IOrderBookSimulator
    {
        /// <summary>
        /// Injects a market data tick into the simulator.
        /// </summary>
        /// <param name="tick">Market data tick</param>
        /// <returns>Resulting trades</returns>
        IReadOnlyList<FillRecord> InjectMarketData(MarketDataTick tick);

        /// <summary>
        /// Submits an order to the simulator.
        /// </summary>
        /// <param name="order">Order to submit</param>
        /// <returns>Resulting fills</returns>
        IReadOnlyList<FillRecord> SubmitOrder(OrderQueueEntry order);

        /// <summary>
        /// Cancels an order.
        /// </summary>
        /// <param name="orderId">Order ID to cancel</param>
        /// <returns>True if canceled successfully</returns>
        bool CancelOrder(long orderId);

        /// <summary>
        /// Amends an order.
        /// </summary>
        /// <param name="orderId">Order ID to amend</param>
        /// <param name="newQuantity">New quantity</param>
        /// <returns>True if amended successfully</returns>
        bool AmendOrder(long orderId, int newQuantity);

        /// <summary>
        /// Gets the current order book state.
        /// </summary>
        OrderBookState GetState();

        /// <summary>
        /// Gets the queue position for an order.
        /// </summary>
        int? GetQueuePosition(long orderId);

        /// <summary>
        /// Gets an estimate of slippage for a hypothetical order.
        /// </summary>
        double EstimateSlippage(OrderQueueEntry order);
    }

    /// <summary>
    /// Immutable order book state snapshot.
    /// </summary>
    public readonly struct OrderBookState : IEquatable<OrderBookState>
    {
        public long InstrumentId { get; init; }
        public long SequenceNumber { get; init; }
        public long Timestamp { get; init; }
        public long BestBidPrice { get; init; }
        public int BestBidSize { get; init; }
        public long BestAskPrice { get; init; }
        public int BestAskSize { get; init; }
        public int OrderCount { get; init; }
        public long TotalBidQuantity { get; init; }
        public long TotalAskQuantity { get; init; }

        public static OrderBookState FromOrderBook(OrderBookEngine book)
        {
            ArgumentNullException.ThrowIfNull(book);
            var bbo = book.BestBidAsk;
            return new OrderBookState
            {
                InstrumentId = book.InstrumentId,
                SequenceNumber = book.SequenceNumber,
                Timestamp = book.CurrentTimestamp,
                BestBidPrice = bbo.BestBidPrice,
                BestBidSize = bbo.BestBidSize,
                BestAskPrice = bbo.BestAskPrice,
                BestAskSize = bbo.BestAskSize,
                OrderCount = book.ActiveOrderCount,
                TotalBidQuantity = book.TotalBidQuantity,
                TotalAskQuantity = book.TotalAskQuantity
            };
        }

        public override bool Equals(object? obj) => obj is OrderBookState other && Equals(other);
        public bool Equals(OrderBookState other) => InstrumentId == other.InstrumentId && SequenceNumber == other.SequenceNumber;
        public override int GetHashCode() => HashCode.Combine(InstrumentId, SequenceNumber);
        public static bool operator ==(OrderBookState left, OrderBookState right) => left.Equals(right);
        public static bool operator !=(OrderBookState left, OrderBookState right) => !left.Equals(right);
    }

    /// <summary>
    /// Result of an order submission.
    /// </summary>
    public readonly struct OrderSubmissionResult : IEquatable<OrderSubmissionResult>
    {
        public long OrderId { get; init; }
        public bool IsAccepted { get; init; }
        public RejectReason? RejectReason { get; init; }
        public IReadOnlyList<FillRecord> Fills { get; init; }
        public int? QueuePosition { get; init; }

        public override bool Equals(object? obj) => obj is OrderSubmissionResult other && Equals(other);
        public bool Equals(OrderSubmissionResult other) => OrderId == other.OrderId && IsAccepted == other.IsAccepted;
        public override int GetHashCode() => HashCode.Combine(OrderId, IsAccepted);
        public static bool operator ==(OrderSubmissionResult left, OrderSubmissionResult right) => left.Equals(right);
        public static bool operator !=(OrderSubmissionResult left, OrderSubmissionResult right) => !left.Equals(right);
    }

    /// <summary>
    /// Configuration for the order book simulator.
    /// </summary>
    public sealed class SimulatorConfig
    {
        /// <summary>Instrument ID for this simulator</summary>
        public long InstrumentId { get; set; } = 1;

        /// <summary>Venue name (for latency simulation)</summary>
        public string Venue { get; set; } = "NASDAQ";

        /// <summary>Seed for deterministic random</summary>
        public int RandomSeed { get; set; } = 12345;

        /// <summary>Enable latency injection</summary>
        public bool EnableLatencyInjection { get; set; } = true;

        /// <summary>Enable audit logging</summary>
        public bool EnableAuditLog { get; set; } = true;

        /// <summary>Enable queue position tracking</summary>
        public bool EnableQueuePosition { get; set; } = true;

        /// <summary>Default time-in-force for orders</summary>
        public TimeInForce DefaultTif { get; set; } = TimeInForce.Day;

        /// <summary>Maximum book depth for queries</summary>
        public int MaxBookDepth { get; set; } = 100;

        /// <summary>Average daily volume for slippage calculations</summary>
        public double AverageDailyVolume { get; set; } = 1_000_000;

        /// <summary>Latency distribution parameters</summary>
        public double LatencyMedianMicroseconds { get; set; } = 50;
        public double LatencyStdDevPercent { get; set; } = 0.3;

        /// <summary>Slippage model parameters</summary>
        public double TemporaryImpactCoefficient { get; set; } = 0.005;
        public double PermanentImpactCoefficient { get; set; } = 0.001;
        public double Volatility { get; set; } = 0.25;
    }

    /// <summary>
    /// Main order book simulator implementation.
    /// 
    /// Features:
    /// - Synchronous market data and order injection
    /// - Per-venue latency simulation
    /// - Queue position and slippage estimation
    /// - Audit logging for deterministic replay
    /// 
    /// Thread safety: Single-threaded simulation (call from one thread).
    /// For multi-threaded use, wrap in a synchronizer.
    /// </summary>
    public sealed class OrderBookSimulator : IOrderBookSimulator, IDisposable
    {
        private readonly OrderBookEngine _book;
        private readonly SimulatorConfig _config;
        private readonly LatencyInjector _latencyInjector;
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _latencyInjector.Dispose();
        }
        private readonly QueuePositionModel _queueModel;
        private readonly SlippageModel _slippageModel;
        private readonly List<FillRecord> _fillBuffer;
        private readonly AuditLog _auditLog;

        /// <summary>
        /// Creates a new order book simulator.
        /// </summary>
        public OrderBookSimulator(SimulatorConfig? config = null)
        {
            _config = config ?? new SimulatorConfig();
            _book = new OrderBookEngine(_config.InstrumentId);
            _latencyInjector = new LatencyInjector(_config.RandomSeed);
            _queueModel = new QueuePositionModel();
            _slippageModel = new SlippageModel
            {
                TemporaryImpactCoefficient = _config.TemporaryImpactCoefficient,
                PermanentImpactCoefficient = _config.PermanentImpactCoefficient,
                Volatility = _config.Volatility
            };
            _fillBuffer = new List<FillRecord>(64);
            _auditLog = new AuditLog();

            if (_config.EnableAuditLog)
            {
                _book.Listener = new AuditLogListener(_auditLog);
            }
        }

        /// <summary>
        /// Gets the underlying order book.
        /// </summary>
        public OrderBookEngine Book => _book;

        /// <summary>
        /// Gets the configuration.
        /// </summary>
        public SimulatorConfig Config => _config;

        // ==================== Market Data Injection ====================

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<FillRecord> InjectMarketData(MarketDataTick tick)
        {
            _fillBuffer.Clear();

            // Apply latency if enabled
            long timestamp = tick.SendTimestampTicks;
            if (_config.EnableLatencyInjection)
            {
                timestamp = _latencyInjector.ApplyLatency(
                    timestamp, _config.Venue, _config.LatencyMedianMicroseconds, _config.LatencyStdDevPercent);
            }

            // Convert tick to orders if needed (for tick-driven simulation)
            // For now, we just update the book state timestamp
            _book.CurrentTimestamp = timestamp;

            // Return empty list (no trades from market data alone)
            return _fillBuffer;
        }

        // ==================== Order Submission ====================

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<FillRecord> SubmitOrder(OrderQueueEntry order)
        {
            _fillBuffer.Clear();

            // Apply latency if enabled
            long timestamp = order.ArrivalTimestamp;
            if (_config.EnableLatencyInjection && timestamp == 0)
            {
                timestamp = _latencyInjector.ApplyLatency(
                    _book.CurrentTimestamp, _config.Venue, 
                    _config.LatencyMedianMicroseconds, _config.LatencyStdDevPercent);
            }

            if (timestamp == 0)
            {
                timestamp = _book.CurrentTimestamp;
            }

            // Set default TIF if not specified
            if (order.TimeInForce == 0)
            {
                order = new OrderQueueEntry(
                    order.OrderId, order.OriginalOrderId, order.InstrumentId,
                    order.Side, order.Price, order.OriginalQuantity, order.LeavesQuantity,
                    order.Type, _config.DefaultTif, order.Flags, order.Status,
                    order.QueuePosition, timestamp, order.ExchangeRef);
            }

            // Process the order
            var fills = _book.ProcessOrder(order, timestamp);
            
            // Copy fills to buffer
            _fillBuffer.AddRange(fills);

            // Update queue model with fills
            foreach (var fill in fills)
            {
                _queueModel.OnTrade(fill.Quantity);
            }

            return _fillBuffer;
        }

        /// <summary>
        /// Submits an order with automatic ID generation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderSubmissionResult SubmitOrder(
            long instrumentId,
            OrderSide side,
            long price,
            int quantity,
            OrderType type = OrderType.Limit,
            OrderAttributes flags = OrderAttributes.None)
        {
            long orderId = OrderIdGenerator.NextId();
            long timestamp = _config.EnableLatencyInjection 
                ? _latencyInjector.ApplyLatency(_book.CurrentTimestamp, _config.Venue)
                : _book.CurrentTimestamp;

            var order = OrderQueueEntry.CreateActive(
                orderId, instrumentId, side, price, quantity,
                type, _config.DefaultTif, flags, timestamp);

            var fills = SubmitOrder(order);

            return new OrderSubmissionResult
            {
                OrderId = orderId,
                IsAccepted = true,
                Fills = fills,
                QueuePosition = GetQueuePosition(orderId)
            };
        }

        // ==================== Order Management ====================

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CancelOrder(long orderId)
        {
            long timestamp = _config.EnableLatencyInjection
                ? _latencyInjector.ApplyLatency(_book.CurrentTimestamp, _config.Venue)
                : _book.CurrentTimestamp;

            return _book.CancelOrder(orderId, timestamp, out _);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AmendOrder(long orderId, int newQuantity)
        {
            long timestamp = _config.EnableLatencyInjection
                ? _latencyInjector.ApplyLatency(_book.CurrentTimestamp, _config.Venue)
                : _book.CurrentTimestamp;

            return _book.AmendOrder(orderId, newQuantity, timestamp, out _);
        }

        // ==================== State Queries ====================

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderBookState GetState()
        {
            return OrderBookState.FromOrderBook(_book);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int? GetQueuePosition(long orderId)
        {
            if (!_config.EnableQueuePosition)
                return null;

            int pos = _book.GetQueuePosition(orderId);
            return pos > 0 ? pos : null;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double EstimateSlippage(OrderQueueEntry order)
        {
            return _slippageModel.CalculateSlippage(order, _book, _config.AverageDailyVolume);
        }

        /// <summary>
        /// Estimates execution price for a hypothetical order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double? EstimateExecutionPrice(OrderSide side, int quantity)
        {
            var dummyOrder = new OrderQueueEntry(
                orderId: -1,
                originalOrderId: -1,
                instrumentId: _config.InstrumentId,
                side: side,
                price: 0,
                originalQuantity: quantity,
                leavesQuantity: quantity,
                type: OrderType.Market,
                timeInForce: _config.DefaultTif,
                flags: OrderAttributes.None,
                status: OrderStatus.Active,
                queuePosition: 0,
                arrivalTimestamp: 0,
                exchangeRef: 0
            );

            return _slippageModel.CalculateExecutionPrice(dummyOrder, _book, _config.AverageDailyVolume);
        }

        // ==================== Queue Position ====================

        /// <summary>
        /// Gets the quantity ahead of an order in the queue.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetQuantityAhead(long orderId)
        {
            return _book.GetQuantityAhead(orderId);
        }

        /// <summary>
        /// Estimates time to fill for an order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double EstimateTimeToFill(long orderId)
        {
            return _queueModel.EstimateTimeToFill(_book, orderId);
        }

        /// <summary>
        /// Estimates fill probability within a time window.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double EstimateFillProbability(long orderId, double timeWindowSeconds)
        {
            return _queueModel.EstimateFillProbability(_book, orderId, timeWindowSeconds);
        }

        // ==================== Book Depth ====================

        /// <summary>
        /// Gets the N best bid levels.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderBookEntry[] GetBestBids(int count)
        {
            var entries = new OrderBookEntry[Math.Min(count, _config.MaxBookDepth)];
            _book.GetBestBids(entries);
            return entries;
        }

        /// <summary>
        /// Gets the N best ask levels.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderBookEntry[] GetBestAsks(int count)
        {
            var entries = new OrderBookEntry[Math.Min(count, _config.MaxBookDepth)];
            _book.GetBestAsks(entries);
            return entries;
        }

        /// <summary>
        /// Gets depth at a specific price level.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BookDepth? GetDepth(long price)
        {
            return _book.GetDepth(price);
        }

        // ==================== Audit & Replay ====================

        public IAuditLogReader AuditLog => _auditLog;

        /// <summary>
        /// Resets the simulator state.
        /// </summary>
        public void Reset()
        {
            _book.CurrentTimestamp = 0;
            LatencyInjector.Reset();
            _fillBuffer.Clear();
        }

        // ==================== Order ID Generator ====================

        private static class OrderIdGenerator
        {
            private static long _counter;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static long NextId()
            {
                return Interlocked.Increment(ref _counter);
            }
        }

        /// <summary>
        /// Listener that forwards events to the audit log.
        /// </summary>
        private sealed class AuditLogListener : IOrderBookListener
        {
            private readonly AuditLog _auditLog;

            public AuditLogListener(AuditLog auditLog)
            {
                _auditLog = auditLog;
            }

            public void OnTrade(FillRecord fill)
            {
                _auditLog.AddTrade(fill);
            }

            public void OnOrderAdded(OrderQueueEntry order)
            {
                _auditLog.AddOrderAdd(order);
            }

            public void OnOrderCanceled(OrderQueueEntry order)
            {
                _auditLog.AddOrderCancel(order);
            }

            public void OnOrderAmended(OrderQueueEntry order)
            {
                _auditLog.AddOrderAmend(order);
            }

            public void OnOrderRejected(OrderQueueEntry order, RejectReason reason)
            {
                _auditLog.AddOrderReject(order, reason);
            }

            public void OnBboChanged(BestBidAsk bbo)
            {
                _auditLog.AddBboChange(bbo);
            }
        }
    }

    /// <summary>
    /// Simple in-memory audit log for deterministic replay.
    /// </summary>
    public sealed class AuditLog : IAuditLogReader
    {
        private readonly List<AuditEvent> _events = new();
        private long _nextSequence = 1;

        public int Count => _events.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOrderAdd(OrderQueueEntry order)
        {
            var evt = AuditEvent.CreateAddEvent(
                Interlocked.Increment(ref _nextSequence),
                order.ArrivalTimestamp,
                order.OrderId,
                order.InstrumentId,
                order.Price,
                order.LeavesQuantity,
                order.Side,
                order.Type,
                order.Flags,
                order.QueuePosition);
            _events.Add(evt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOrderCancel(OrderQueueEntry order)
        {
            var evt = AuditEvent.CreateCancelEvent(
                Interlocked.Increment(ref _nextSequence),
                order.ArrivalTimestamp,
                order.OrderId,
                order.InstrumentId,
                0,
                order.OriginalQuantity);
            _events.Add(evt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOrderAmend(OrderQueueEntry order)
        {
            var evt = AuditEvent.CreateAmendEvent(
                Interlocked.Increment(ref _nextSequence),
                order.ArrivalTimestamp,
                order.OrderId,
                order.InstrumentId,
                order.LeavesQuantity,
                order.OriginalQuantity,
                order.Price,
                order.Price);
            _events.Add(evt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddTrade(FillRecord fill)
        {
            var evt = AuditEvent.CreateTradeEvent(
                fill.SequenceNumber,
                fill.Timestamp,
                fill.InstrumentId,
                fill.Price,
                fill.Quantity,
                fill.AggressorOrderId,
                fill.PassiveOrderId);
            _events.Add(evt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOrderReject(OrderQueueEntry order, RejectReason reason)
        {
            var evt = AuditEvent.CreateRejectEvent(
                Interlocked.Increment(ref _nextSequence),
                order.ArrivalTimestamp,
                order.OrderId,
                order.InstrumentId,
                reason,
                order.OriginalQuantity,
                order.Price);
            _events.Add(evt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddBboChange(BestBidAsk bbo)
        {
            var evt = AuditEvent.CreateBboChangeEvent(
                Interlocked.Increment(ref _nextSequence),
                bbo.Timestamp,
                0, // Instrument ID not stored in BBO
                bbo.BestBidPrice,
                bbo.BestBidSize,
                bbo.BestAskPrice,
                bbo.BestAskSize);
            _events.Add(evt);
        }

        public IReadOnlyList<AuditEvent> Events => _events;

        /// <summary>
        /// Clears the log.
        /// </summary>
        public void Clear()
        {
            _events.Clear();
            _nextSequence = 1;
        }
    }

    /// <summary>
    /// Interface for reading audit logs.
    /// </summary>
    public interface IAuditLogReader
    {
        int Count { get; }
        IReadOnlyList<AuditEvent> Events { get; }
        void Clear();
    }
}

