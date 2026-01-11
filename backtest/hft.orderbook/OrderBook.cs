using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Hft.OrderBook
{
    /// <summary>
    /// Core order book implementation with price-time priority matching.
    /// Supports L2 (price levels) and L3 (individual orders) functionality.
    /// 
    /// Key features:
    /// - Deterministic price-time priority matching
    /// - Queue position tracking for passive orders
    /// - Hidden order support (icesberg, fully hidden)
    /// - Post-only and reduce-only order handling
    /// - Snapshot and restore for checkpointing
    /// 
    /// Performance: Optimized for millions of ticks/hour with cache line alignment.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 64)]
    public sealed class OrderBook
    {
        // ==================== Hot Path: Instrument Identity ====================
        // Isolated on its own cache line
        private readonly long _instrumentPadding1;
        private readonly long _instrumentPadding2;
        private readonly long _instrumentPadding3;
        private readonly long _instrumentPadding4;
        private readonly long _instrumentPadding5;
        private readonly long _instrumentPadding6;
        private readonly long _instrumentPadding7;
        private readonly long _instrumentPadding8;

        /// <summary>Instrument/symbol ID</summary>
        public long InstrumentId { get; }

        // ==================== Hot Path: Sequence & Timestamp ====================
        private readonly long _seqPadding1;
        private readonly long _seqPadding2;
        private readonly long _seqPadding3;
        private readonly long _seqPadding4;
        private readonly long _seqPadding5;
        private readonly long _seqPadding6;
        private readonly long _seqPadding7;
        private readonly long _seqPadding8;

        /// <summary>Monotonically increasing sequence number</summary>
        public long SequenceNumber { get; private set; }

        /// <summary>Current timestamp (microseconds)</summary>
        public long CurrentTimestamp { get; private set; }

        // ==================== Hot Path: BBO ====================
        private readonly long _bboPadding1;
        private readonly long _bboPadding2;
        private readonly long _bboPadding3;
        private readonly long _bboPadding4;
        private readonly long _bboPadding5;
        private readonly long _bboPadding6;
        private readonly long _bboPadding7;
        private readonly long _bboPadding8;

        /// <summary>Best bid price level (null if no bids)</summary>
        public PriceLevel? BestBidLevel { get; private set; }

        /// <summary>Best ask price level (null if no asks)</summary>
        public PriceLevel? BestAskLevel { get; private set; }

        // ==================== Hot Path: Statistics ====================
        private readonly long _statsPadding1;
        private readonly long _statsPadding2;
        private readonly long _statsPadding3;
        private readonly long _statsPadding4;
        private readonly long _statsPadding5;
        private readonly long _statsPadding6;
        private readonly long _statsPadding7;
        private readonly long _statsPadding8;

        /// <summary>Total number of active orders</summary>
        public int ActiveOrderCount { get; private set; }

        /// <summary>Total buy-side quantity</summary>
        public long TotalBidQuantity { get; private set; }

        /// <summary>Total sell-side quantity</summary>
        public long TotalAskQuantity { get; private set; }

        // ==================== Order Storage (L3) ====================
        private readonly long _orderMapPadding1;
        private readonly long _orderMapPadding2;
        private readonly long _orderMapPadding3;
        private readonly long _orderMapPadding4;
        private readonly long _orderMapPadding5;
        private readonly long _orderMapPadding6;
        private readonly long _orderMapPadding7;
        private readonly long _orderMapPadding8;

        /// <summary>Order lookup by OrderId (O(1) access)</summary>
        private readonly Dictionary<long, OrderQueueNode> _orderMap;

        /// <summary>Node pool for memory efficiency</summary>
        private readonly OrderQueueNodePool _nodePool;

        // ==================== Price Level Storage ====================
        private readonly long _priceLevelPadding1;
        private readonly long _priceLevelPadding2;
        private readonly long _priceLevelPadding3;
        private readonly long _priceLevelPadding4;
        private readonly long _priceLevelPadding5;
        private readonly long _priceLevelPadding6;
        private readonly long _priceLevelPadding7;
        private readonly long _priceLevelPadding8;

        /// <summary>Buy price levels (sorted descending, best bid first)</summary>
        private readonly SortedDictionary<long, PriceLevel> _bidLevels;

        /// <summary>Sell price levels (sorted ascending, best ask first)</summary>
        private readonly SortedDictionary<long, PriceLevel> _askLevels;

        // ==================== Event Callbacks ====================
        private readonly long _callbackPadding1;
        private readonly long _callbackPadding2;
        private readonly long _callbackPadding3;
        private readonly long _callbackPadding4;
        private readonly long _callbackPadding5;
        private readonly long _callbackPadding6;
        private readonly long _callbackPadding7;
        private readonly long _callbackPadding8;

        /// <summary>Listener for order book events (optional)</summary>
        public IOrderBookListener? Listener { get; set; }

        /// <summary>Seed for deterministic random (if needed)</summary>
        private readonly int _randomSeed;

        /// <summary>Deterministic random for seeded operations</summary>
        private Random? _random;

        // ==================== Constructor ====================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderBook(long instrumentId, int randomSeed = 12345)
        {
            InstrumentId = instrumentId;
            SequenceNumber = 0;
            CurrentTimestamp = 0;
            BestBidLevel = null;
            BestAskLevel = null;
            ActiveOrderCount = 0;
            TotalBidQuantity = 0;
            TotalAskQuantity = 0;
            _orderMap = new Dictionary<long, OrderQueueNode>(1024);
            _nodePool = new OrderQueueNodePool(1024 * 16); // 16K node pool
            _bidLevels = new SortedDictionary<long, PriceLevel>(Comparer<long>.Create((a, b) => b.CompareTo(a))); // Descending
            _askLevels = new SortedDictionary<long, PriceLevel>(); // Ascending
            _randomSeed = randomSeed;
            _random = null;

            // Initialize padding
            _instrumentPadding1 = _instrumentPadding2 = _instrumentPadding3 = _instrumentPadding4 = 
                _instrumentPadding5 = _instrumentPadding6 = _instrumentPadding7 = _instrumentPadding8 = 0;
            _seqPadding1 = _seqPadding2 = _seqPadding3 = _seqPadding4 = 
                _seqPadding5 = _seqPadding6 = _seqPadding7 = _seqPadding8 = 0;
            _bboPadding1 = _bboPadding2 = _bboPadding3 = _bboPadding4 = 
                _bboPadding5 = _bboPadding6 = _bboPadding7 = _bboPadding8 = 0;
            _statsPadding1 = _statsPadding2 = _statsPadding3 = _statsPadding4 = 
                _statsPadding5 = _statsPadding6 = _statsPadding7 = _statsPadding8 = 0;
            _orderMapPadding1 = _orderMapPadding2 = _orderMapPadding3 = _orderMapPadding4 = 
                _orderMapPadding5 = _orderMapPadding6 = _orderMapPadding7 = _orderMapPadding8 = 0;
            _priceLevelPadding1 = _priceLevelPadding2 = _priceLevelPadding3 = _priceLevelPadding4 = 
                _priceLevelPadding5 = _priceLevelPadding6 = _priceLevelPadding7 = _priceLevelPadding8 = 0;
            _callbackPadding1 = _callbackPadding2 = _callbackPadding3 = _callbackPadding4 = 
                _callbackPadding5 = _callbackPadding6 = _callbackPadding7 = _callbackPadding8 = 0;
        }

        // ==================== Core Matching Methods ====================

        /// <summary>
        /// Processes a new order and returns any resulting fills.
        /// Implements price-time priority matching with queue position tracking.
        /// </summary>
        /// <param name="order">The order to process</param>
        /// <param name="timestamp">Event timestamp (microseconds)</param>
        /// <returns>List of resulting fills (may be empty)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<FillRecord> ProcessOrder(OrderQueueEntry order, long timestamp)
        {
            CurrentTimestamp = timestamp;
            var fills = new List<FillRecord>();
            var incomingOrder = order;

            // Validate order
            if (!ValidateOrder(incomingOrder, out var rejectReason))
            {
                RejectOrder(incomingOrder, rejectReason, timestamp);
                return fills;
            }

            // Market order handling
            if (incomingOrder.Type == OrderType.Market)
            {
                ProcessMarketOrder(incomingOrder, timestamp, fills);
                return fills;
            }

            // Limit order handling
            if (incomingOrder.Side == OrderSide.Buy)
            {
                ProcessBuyOrder(incomingOrder, timestamp, fills);
            }
            else
            {
                ProcessSellOrder(incomingOrder, timestamp, fills);
            }

            return fills;
        }

        /// <summary>
        /// Processes a market order (aggressive).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessMarketOrder(OrderQueueEntry order, long timestamp, List<FillRecord> fills)
        {
            if (order.Side == OrderSide.Buy)
            {
                // Buy market order: match against asks (lowest first)
                while (order.LeavesQuantity > 0 && BestAskLevel != null)
                {
                    var matchResult = MatchAtLevel(BestAskLevel, order, timestamp, isAggressor: true);
                    if (matchResult.FilledQuantity == 0) break;

                    fills.AddRange(matchResult.Fills);
                    order = order.WithLeavesQuantity(order.LeavesQuantity - matchResult.FilledQuantity);

                    // Update BBO if needed
                    if (BestAskLevel.IsEmpty)
                    {
                        RemovePriceLevel(BestAskLevel, isBid: false);
                        BestAskLevel = _askLevels.Count > 0 ? _askLevels.First.Value : null;
                    }
                }
            }
            else
            {
                // Sell market order: match against bids (highest first)
                while (order.LeavesQuantity > 0 && BestBidLevel != null)
                {
                    var matchResult = MatchAtLevel(BestBidLevel, order, timestamp, isAggressor: true);
                    if (matchResult.FilledQuantity == 0) break;

                    fills.AddRange(matchResult.Fills);
                    order = order.WithLeavesQuantity(order.LeavesQuantity - matchResult.FilledQuantity);

                    // Update BBO if needed
                    if (BestBidLevel.IsEmpty)
                    {
                        RemovePriceLevel(BestBidLevel, isBid: true);
                        BestBidLevel = _bidLevels.Count > 0 ? _bidLevels.First.Value : null;
                    }
                }
            }

            // If market order not fully filled, remaining quantity is lost
            if (order.LeavesQuantity > 0)
            {
                // Market orders can be unfilled - this is expected behavior
                SequenceNumber++;
            }
        }

        /// <summary>
        /// Processes a buy limit order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessBuyOrder(OrderQueueEntry order, long timestamp, List<FillRecord> fills)
        {
            // Check if order crosses the spread (would take liquidity)
            if (BestAskLevel != null && order.Price >= BestAskLevel.Price)
            {
                // Post-only check
                if (order.IsPostOnly)
                {
                    // Reject - would cross
                    RejectOrder(order, RejectReason.PostOnlyWouldTake, timestamp);
                    return;
                }

                // Aggressive: match against asks starting from best
                int remainingQty = order.LeavesQuantity;
                var currentLevel = BestAskLevel;

                while (remainingQty > 0 && currentLevel != null)
                {
                    var matchResult = MatchAtLevel(currentLevel, order, timestamp, isAggressor: true);
                    if (matchResult.FilledQuantity == 0)
                    {
                        currentLevel = currentLevel.Next; // Move to next ask level
                        continue;
                    }

                    fills.AddRange(matchResult.Fills);
                    remainingQty -= matchResult.FilledQuantity;

                    // Remove empty price levels
                    if (currentLevel.IsEmpty)
                    {
                        var nextLevel = currentLevel.Next;
                        RemovePriceLevel(currentLevel, isBid: false);
                        currentLevel = nextLevel ?? (_askLevels.Count > 0 ? _askLevels.First.Value : null);
                    }
                }

                // Update BBO
                BestAskLevel = _askLevels.Count > 0 ? _askLevels.First.Value : null;

                // If order has remaining quantity, add to book
                if (remainingQty > 0)
                {
                    AddOrderToBook(order.WithLeavesQuantity(remainingQty), timestamp);
                }
            }
            else
            {
                // Passive: add to bid side
                AddOrderToBook(order, timestamp);
            }
        }

        /// <summary>
        /// Processes a sell limit order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessSellOrder(OrderQueueEntry order, long timestamp, List<FillRecord> fills)
        {
            // Check if order crosses the spread (would take liquidity)
            if (BestBidLevel != null && order.Price <= BestBidLevel.Price)
            {
                // Post-only check
                if (order.IsPostOnly)
                {
                    RejectOrder(order, RejectReason.PostOnlyWouldTake, timestamp);
                    return;
                }

                // Aggressive: match against bids starting from best
                int remainingQty = order.LeavesQuantity;
                var currentLevel = BestBidLevel;

                while (remainingQty > 0 && currentLevel != null)
                {
                    var matchResult = MatchAtLevel(currentLevel, order, timestamp, isAggressor: true);
                    if (matchResult.FilledQuantity == 0)
                    {
                        currentLevel = currentLevel.Next; // Move to next bid level
                        continue;
                    }

                    fills.AddRange(matchResult.Fills);
                    remainingQty -= matchResult.FilledQuantity;

                    // Remove empty price levels
                    if (currentLevel.IsEmpty)
                    {
                        var nextLevel = currentLevel.Next;
                        RemovePriceLevel(currentLevel, isBid: true);
                        currentLevel = nextLevel ?? (_bidLevels.Count > 0 ? _bidLevels.First.Value : null);
                    }
                }

                // Update BBO
                BestBidLevel = _bidLevels.Count > 0 ? _bidLevels.First.Value : null;

                // If order has remaining quantity, add to book
                if (remainingQty > 0)
                {
                    AddOrderToBook(order.WithLeavesQuantity(remainingQty), timestamp);
                }
            }
            else
            {
                // Passive: add to ask side
                AddOrderToBook(order, timestamp);
            }
        }

        /// <summary>
        /// Matches an order against a price level (FIFO within price).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MatchResult MatchAtLevel(PriceLevel level, OrderQueueEntry order, long timestamp, bool isAggressor)
        {
            var fills = new List<FillRecord>();
            int remainingQty = order.LeavesQuantity;
            long sequenceNumber = SequenceNumber;
            int filledQty = 0;

            // Match against orders in queue (visible first, then hidden)
            var node = level.PeekFront();
            
            while (remainingQty > 0 && node != null)
            {
                int matchQty = Math.Min(remainingQty, node.Order.LeavesQuantity);

                // Create fill record
                var fill = new FillRecord(
                    fillId: sequenceNumber,
                    sequenceNumber: sequenceNumber,
                    timestamp: timestamp,
                    aggressorOrderId: order.OrderId,
                    passiveOrderId: node.Order.OrderId,
                    instrumentId: InstrumentId,
                    price: level.Price,
                    quantity: matchQty,
                    side: order.Side,
                    isHidden: node.IsHidden,
                    isMidPoint: false,
                    liquidity: LiquidityType.Taker
                );

                fills.Add(fill);
                sequenceNumber++;
                filledQty += matchQty;
                remainingQty -= matchQty;

                // Update the passive order
                var passiveOrder = node.Order;
                int oldQty = passiveOrder.LeavesQuantity;
                int newQty = passiveOrder.LeavesQuantity - matchQty;

                if (newQty == 0)
                {
                    // Order fully filled - remove from book
                    var nextNode = node.Next;
                    level.RemoveOrder(node, oldQty);
                    _orderMap.Remove(node.Order.OrderId);
                    _nodePool.Return(node);
                    ActiveOrderCount--;
                    node = nextNode;
                }
                else
                {
                    // Partial fill - update order
                    var updatedOrder = passiveOrder.WithLeavesQuantity(newQty);
                    node.UpdateOrder(updatedOrder);
                    level.UpdateQuantity(oldQty, newQty, node.IsHidden);
                    node = node.Next;
                }

                // Update statistics
                if (order.Side == OrderSide.Buy)
                {
                    TotalAskQuantity -= matchQty;
                }
                else
                {
                    TotalBidQuantity -= matchQty;
                }

                // Notify listener
                Listener?.OnTrade(fill);
            }

            SequenceNumber = sequenceNumber;

            return new MatchResult
            {
                Fills = fills,
                FilledQuantity = filledQty
            };
        }

        /// <summary>
        /// Adds an order to the book (passive limit order).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddOrderToBook(OrderQueueEntry order, long timestamp)
        {
            // Get or create price level
            PriceLevel? level;
            
            if (order.Side == OrderSide.Buy)
            {
                if (!_bidLevels.TryGetValue(order.Price, out level))
                {
                    level = new PriceLevel(order.Price);
                    _bidLevels[order.Price] = level;
                    
                    // Update BBO if this is new best bid
                    if (BestBidLevel == null || order.Price > BestBidLevel.Price)
                    {
                        BestBidLevel = level;
                        NotifyBboChange();
                    }
                }
            }
            else
            {
                if (!_askLevels.TryGetValue(order.Price, out level))
                {
                    level = new PriceLevel(order.Price);
                    _askLevels[order.Price] = level;
                    
                    // Update BBO if this is new best ask
                    if (BestAskLevel == null || order.Price < BestAskLevel.Price)
                    {
                        BestAskLevel = level;
                        NotifyBboChange();
                    }
                }
            }

            // Allocate node from pool
            var node = _nodePool.Rent(order);

            // Set queue position (at the back of the queue)
            int queuePosition = level.TotalOrderCount + 1;
            var orderWithPosition = order.WithQueuePosition(queuePosition);
            node.UpdateOrder(orderWithPosition);

            // Add to level queue
            level.AddOrder(orderWithPosition, node);

            // Add to order map
            _orderMap[order.OrderId] = node;

            // Update statistics
            ActiveOrderCount++;
            if (order.Side == OrderSide.Buy)
            {
                TotalBidQuantity += order.LeavesQuantity;
            }
            else
            {
                TotalAskQuantity += order.LeavesQuantity;
            }

            // Log event
            SequenceNumber++;
            var addEvent = AuditEvent.CreateAddEvent(
                SequenceNumber, timestamp, order.OrderId, InstrumentId,
                order.Price, order.LeavesQuantity, order.Side, order.Type, order.Flags, queuePosition);
            Listener?.OnOrderAdded(orderWithPosition);
        }

        /// <summary>
        /// Cancels an order by OrderId.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CancelOrder(long orderId, long timestamp, out OrderQueueEntry? canceledOrder)
        {
            canceledOrder = null;

            if (!_orderMap.TryGetValue(orderId, out var node))
                return false;

            var order = node.Order;
            canceledOrder = order;

            // Get the price level
            var level = node.PriceLevel;
            if (level != null)
            {
                // Remove from queue
                level.RemoveOrder(node, order.LeavesQuantity);
                TotalAskQuantity -= order.LeavesQuantity;

                // Update BBO if needed
                if (level.IsEmpty)
                {
                    if (order.Side == OrderSide.Buy)
                    {
                        RemovePriceLevel(level, isBid: true);
                        BestBidLevel = _bidLevels.Count > 0 ? _bidLevels.First.Value : null;
                    }
                    else
                    {
                        RemovePriceLevel(level, isBid: false);
                        BestAskLevel = _askLevels.Count > 0 ? _askLevels.First.Value : null;
                    }
                    NotifyBboChange();
                }
            }

            // Remove from map and return node to pool
            _orderMap.Remove(orderId);
            _nodePool.Return(node);
            ActiveOrderCount--;

            // Log event
            SequenceNumber++;
            var cancelEvent = AuditEvent.CreateCancelEvent(
                SequenceNumber, timestamp, orderId, InstrumentId, 0, order.OriginalQuantity);
            
            Listener?.OnOrderCanceled(order);

            return true;
        }

        /// <summary>
        /// Amends an order (quantity change only).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AmendOrder(long orderId, int newQuantity, long timestamp, out OrderQueueEntry? amendedOrder)
        {
            amendedOrder = null;

            if (!_orderMap.TryGetValue(orderId, out var node))
                return false;

            var oldOrder = node.Order;
            var level = node.PriceLevel;
            int oldQty = oldOrder.LeavesQuantity;

            // Validate new quantity
            if (newQuantity <= 0 || newQuantity > oldOrder.OriginalQuantity)
                return false;

            // Update level quantities
            if (level != null)
            {
                level.UpdateQuantity(oldQty, newQuantity, node.IsHidden);

                if (oldOrder.Side == OrderSide.Buy)
                {
                    TotalBidQuantity += (newQuantity - oldQty);
                }
                else
                {
                    TotalAskQuantity += (newQuantity - oldQty);
                }
            }

            // Create amended order
            var amended = new OrderQueueEntry(
                orderId: oldOrder.OrderId,
                originalOrderId: oldOrder.OriginalOrderId,
                instrumentId: oldOrder.InstrumentId,
                side: oldOrder.Side,
                price: oldOrder.Price,
                originalQuantity: oldOrder.OriginalQuantity,
                leavesQuantity: newQuantity,
                type: oldOrder.Type,
                timeInForce: oldOrder.TimeInForce,
                flags: oldOrder.Flags,
                status: newQuantity == oldOrder.OriginalQuantity ? OrderStatus.Active : OrderStatus.PartiallyFilled,
                queuePosition: node.GetQueuePosition(),
                arrivalTimestamp: oldOrder.ArrivalTimestamp,
                exchangeRef: oldOrder.ExchangeRef
            );

            node.UpdateOrder(amended);
            amendedOrder = amended;

            // Update statistics
            if (oldOrder.Side == OrderSide.Buy)
            {
                TotalBidQuantity += (newQuantity - oldQty);
            }
            else
            {
                TotalAskQuantity += (newQuantity - oldQty);
            }

            // Log event
            SequenceNumber++;
            var amendEvent = AuditEvent.CreateAmendEvent(
                SequenceNumber, timestamp, orderId, InstrumentId,
                newQuantity, oldQty, oldOrder.Price, oldOrder.Price);
            
            Listener?.OnOrderAmended(amended);

            return true;
        }

        // ==================== Queue Position Methods ====================

        /// <summary>
        /// Gets the current queue position for an order.
        /// Returns 1-based position (1 = front of queue).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetQueuePosition(long orderId)
        {
            if (!_orderMap.TryGetValue(orderId, out var node))
                return -1;

            return node.GetQueuePosition();
        }

        /// <summary>
        /// Gets the quantity ahead of an order in the queue.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetQuantityAhead(long orderId)
        {
            if (!_orderMap.TryGetValue(orderId, out var node))
                return -1;

            return node.GetQuantityAhead();
        }

        /// <summary>
        /// Gets the number of orders ahead of an order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetOrdersAhead(long orderId)
        {
            if (!_orderMap.TryGetValue(orderId, out var node))
                return -1;

            return node.GetOrdersAhead();
        }

        /// <summary>
        /// Gets an order by OrderId.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderQueueEntry? GetOrder(long orderId)
        {
            if (_orderMap.TryGetValue(orderId, out var node))
                return node.Order;

            return null;
        }

        // ==================== Book State Queries ====================

        /// <summary>
        /// Gets the current best bid and ask.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BestBidAsk GetBestBidAsk()
        {
            return new BestBidAsk(
                BestBidLevel?.Price ?? 0,
                BestBidLevel?.VisibleQuantity ?? 0,
                BestAskLevel?.Price ?? 0,
                BestAskLevel?.VisibleQuantity ?? 0,
                CurrentTimestamp,
                SequenceNumber
            );
        }

        /// <summary>
        /// Gets the N best bid levels.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetBestBids(Span<OrderBookEntry> entries)
        {
            int index = 0;
            var level = BestBidLevel;
            long seq = SequenceNumber;

            while (index < entries.Length && level != null)
            {
                entries[index] = level.GetEntry(seq);
                level = level.Next;
                index++;
            }
        }

        /// <summary>
        /// Gets the N best ask levels.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetBestAsks(Span<OrderBookEntry> entries)
        {
            int index = 0;
            var level = BestAskLevel;
            long seq = SequenceNumber;

            while (index < entries.Length && level != null)
            {
                entries[index] = level.GetEntry(seq);
                level = level.Next;
                index++;
            }
        }

        /// <summary>
        /// Gets the depth at a specific price level.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BookDepth? GetDepth(long price)
        {
            if (_bidLevels.TryGetValue(price, out var bidLevel) ||
                _askLevels.TryGetValue(price, out var askLevel))
            {
                return new BookDepth(
                    price: price,
                    bidDepth: bidLevel?.VisibleQuantity ?? 0,
                    askDepth: askLevel?.VisibleQuantity ?? 0,
                    bidOrders: bidLevel?.VisibleOrderCount ?? 0,
                    askOrders: askLevel?.VisibleOrderCount ?? 0,
                    bidHidden: bidLevel?.HiddenOrderCount ?? 0,
                    askHidden: askLevel?.HiddenOrderCount ?? 0
                );
            }

            return null;
        }

        // ==================== Validation & Helper Methods ====================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ValidateOrder(OrderQueueEntry order, out RejectReason reason)
        {
            reason = RejectReason.Unknown;

            if (order.OrderId <= 0)
            {
                reason = RejectReason.InvalidOrderId;
                return false;
            }

            if (order.Price < 0)
            {
                reason = RejectReason.InvalidPrice;
                return false;
            }

            if (order.LeavesQuantity <= 0)
            {
                reason = RejectReason.InvalidQuantity;
                return false;
            }

            // Check if order already exists
            if (_orderMap.ContainsKey(order.OrderId))
            {
                reason = RejectReason.InvalidOrderId;
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RejectOrder(OrderQueueEntry order, RejectReason reason, long timestamp)
        {
            SequenceNumber++;
            var rejectEvent = AuditEvent.CreateRejectEvent(
                SequenceNumber, timestamp, order.OrderId, InstrumentId,
                reason, order.OriginalQuantity, order.Price);
            
            Listener?.OnOrderRejected(order, reason);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemovePriceLevel(PriceLevel level, bool isBid)
        {
            if (isBid)
            {
                _bidLevels.Remove(level.Price);
            }
            else
            {
                _askLevels.Remove(level.Price);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void NotifyBboChange()
        {
            var bbo = GetBestBidAsk();
            Listener?.OnBboChanged(bbo);
        }

        // ==================== Snapshot & Restore ====================

        /// <summary>
        /// Creates a snapshot of the current order book state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderBookSnapshot CreateSnapshot()
        {
            return new OrderBookSnapshot(
                version: OrderBookSnapshot.CurrentVersion,
                instrumentId: InstrumentId,
                timestamp: CurrentTimestamp,
                sequenceNumber: SequenceNumber,
                bestBidPrice: BestBidLevel?.Price ?? 0,
                bestBidSize: BestBidLevel?.VisibleQuantity ?? 0,
                bestAskPrice: BestAskLevel?.Price ?? 0,
                bestAskSize: BestAskLevel?.VisibleQuantity ?? 0,
                bidLevelCount: _bidLevels.Count,
                askLevelCount: _askLevels.Count,
                totalOrderCount: ActiveOrderCount,
                totalBidQuantity: TotalBidQuantity,
                totalAskQuantity: TotalAskQuantity
            );
        }

        /// <summary>
        /// Gets the order count for statistics.
        /// </summary>
        public int OrderCount => _orderMap.Count;
    }

    /// <summary>
    /// Result of matching at a price level.
    /// </summary>
    public readonly struct MatchResult
    {
        public List<FillRecord> Fills { get; init; }
        public int FilledQuantity { get; init; }
    }
}

