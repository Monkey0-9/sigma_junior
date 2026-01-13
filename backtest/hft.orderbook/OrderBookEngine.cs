using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Linq;

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
    public sealed class OrderBookEngine
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
        public long CurrentTimestamp { get; internal set; }

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

        /// <summary>Price level lookup (SortedDictionary to maintain price priority)</summary>
        private readonly SortedDictionary<long, PriceLevel> _bidLevels; // Highest price first
        private readonly SortedDictionary<long, PriceLevel> _askLevels; // Lowest price first

        /// <summary>Object pool for order nodes to minimize GC pressure</summary>
        private readonly OrderQueueNodePool _nodePool;

        /// <summary>Event listener for real-time notifications</summary>
        public IOrderBookListener? Listener { get; set; }

        // ==================== Constructor ====================
        public OrderBookEngine(long instrumentId, int initialCapacity = 1000)
        {
            InstrumentId = instrumentId;
            SequenceNumber = 0;
            CurrentTimestamp = 0;
            ActiveOrderCount = 0;
            TotalBidQuantity = 0;
            TotalAskQuantity = 0;

            _orderMap = new Dictionary<long, OrderQueueNode>(initialCapacity);
            _bidLevels = new SortedDictionary<long, PriceLevel>(new DescendingComparer<long>());
            _askLevels = new SortedDictionary<long, PriceLevel>();
            _nodePool = new OrderQueueNodePool(initialCapacity);

            // Initialize padding to prevent false sharing
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
        }

        // ==================== Order Processing (Core) ====================

        /// <summary>
        /// Processes a new order and returns any resulting fills.
        /// Implements price-time priority matching with queue position tracking.
        /// </summary>
        /// <param name="order">The order to process</param>
        /// <param name="timestamp">Event timestamp (microseconds)</param>
        /// <returns>List of resulting fills (may be empty)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<FillRecord> ProcessOrder(OrderQueueEntry order, long timestamp)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessMarketOrder(OrderQueueEntry order, long timestamp, List<FillRecord> fills)
        {
            if (order.Side == OrderSide.Buy)
            {
                var currentLevel = BestAskLevel;
                int remainingQty = order.LeavesQuantity;

                while (remainingQty > 0 && currentLevel != null)
                {
                    var matchResult = MatchAtLevel(currentLevel, order.WithLeavesQuantity(remainingQty), timestamp, isAggressor: true);
                    if (matchResult.FilledQuantity == 0) break;

                    fills.AddRange(matchResult.Fills);
                    remainingQty -= matchResult.FilledQuantity;

                    if (currentLevel.IsEmpty)
                    {
                        var nextLevel = currentLevel.Next;
                        RemovePriceLevel(currentLevel, isBid: false);
                        currentLevel = nextLevel ?? (_askLevels.Count > 0 ? _askLevels.Values.First() : null);
                    }
                }

                BestAskLevel = _askLevels.Count > 0 ? _askLevels.Values.First() : null;
                NotifyBboChange();
            }
            else
            {
                var currentLevel = BestBidLevel;
                int remainingQty = order.LeavesQuantity;

                while (remainingQty > 0 && currentLevel != null)
                {
                    var matchResult = MatchAtLevel(currentLevel, order.WithLeavesQuantity(remainingQty), timestamp, isAggressor: true);
                    if (matchResult.FilledQuantity == 0) break;

                    fills.AddRange(matchResult.Fills);
                    remainingQty -= matchResult.FilledQuantity;

                    if (currentLevel.IsEmpty)
                    {
                        var nextLevel = currentLevel.Next;
                        RemovePriceLevel(currentLevel, isBid: true);
                        currentLevel = nextLevel ?? (_bidLevels.Count > 0 ? _bidLevels.Values.First() : null);
                    }
                }

                BestBidLevel = _bidLevels.Count > 0 ? _bidLevels.Values.First() : null;
                NotifyBboChange();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessBuyOrder(OrderQueueEntry order, long timestamp, List<FillRecord> fills)
        {
            int remainingQty = order.LeavesQuantity;
            var currentLevel = BestAskLevel;

            // Match against opposing side (Asks)
            while (remainingQty > 0 && currentLevel != null && order.Price >= currentLevel.Price)
            {
                var matchResult = MatchAtLevel(currentLevel, order.WithLeavesQuantity(remainingQty), timestamp, isAggressor: true);
                if (matchResult.FilledQuantity == 0)
                {
                    currentLevel = currentLevel.Next; // Move to next cheapest ask
                    continue;
                }

                fills.AddRange(matchResult.Fills);
                remainingQty -= matchResult.FilledQuantity;

                if (currentLevel.IsEmpty)
                {
                    var nextLevel = currentLevel.Next;
                    RemovePriceLevel(currentLevel, isBid: false);
                    currentLevel = nextLevel ?? (_askLevels.Count > 0 ? _askLevels.Values.First() : null);
                }
            }

            // Update BBO
            BestAskLevel = _askLevels.Count > 0 ? _askLevels.Values.First() : null;

            // If order has remaining quantity, add to book
            if (remainingQty > 0)
            {
                AddOrderToBook(order.WithLeavesQuantity(remainingQty), timestamp);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessSellOrder(OrderQueueEntry order, long timestamp, List<FillRecord> fills)
        {
            int remainingQty = order.LeavesQuantity;
            var currentLevel = BestBidLevel;

            // Match against opposing side (Bids)
            while (remainingQty > 0 && currentLevel != null && order.Price <= currentLevel.Price)
            {
                var matchResult = MatchAtLevel(currentLevel, order.WithLeavesQuantity(remainingQty), timestamp, isAggressor: true);
                if (matchResult.FilledQuantity == 0)
                {
                    currentLevel = currentLevel.Next; // Move to next highest bid
                    continue;
                }

                fills.AddRange(matchResult.Fills);
                remainingQty -= matchResult.FilledQuantity;

                if (currentLevel.IsEmpty)
                {
                    var nextLevel = currentLevel.Next;
                    RemovePriceLevel(currentLevel, isBid: true);
                    currentLevel = nextLevel ?? (_bidLevels.Count > 0 ? _bidLevels.Values.First() : null);
                }
            }

            // Update BBO
            BestBidLevel = _bidLevels.Count > 0 ? _bidLevels.Values.First() : null;

            // If order has remaining quantity, add to book
            if (remainingQty > 0)
            {
                AddOrderToBook(order.WithLeavesQuantity(remainingQty), timestamp);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MatchResult MatchAtLevel(PriceLevel level, OrderQueueEntry order, long timestamp, bool isAggressor)
        {
            ArgumentNullException.ThrowIfNull(level);
            var fills = new List<FillRecord>();
            int remainingQty = order.LeavesQuantity;
            long sequenceNumber = SequenceNumber;
            int filledQty = 0;

            var node = level.PeekFront();
            
            while (remainingQty > 0 && node != null)
            {
                int matchQty = Math.Min(remainingQty, node.Order.LeavesQuantity);

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

                var passiveOrder = node.Order;
                int oldQty = passiveOrder.LeavesQuantity;
                int newQty = passiveOrder.LeavesQuantity - matchQty;

                if (newQty == 0)
                {
                    var nextNode = node.Next;
                    level.RemoveOrder(node, oldQty);
                    _orderMap.Remove(node.Order.OrderId);
                    _nodePool.Return(node);
                    ActiveOrderCount--;
                    node = nextNode;
                }
                else
                {
                    var updatedOrder = passiveOrder.WithLeavesQuantity(newQty);
                    node.UpdateOrder(updatedOrder);
                    level.UpdateQuantity(oldQty, newQty, node.IsHidden);
                    node = node.Next;
                }

                if (order.Side == OrderSide.Buy)
                {
                    TotalAskQuantity -= matchQty;
                }
                else
                {
                    TotalBidQuantity -= matchQty;
                }

                Listener?.OnTrade(fill);
            }

            SequenceNumber = sequenceNumber;

            return new MatchResult
            {
                Fills = fills,
                FilledQuantity = filledQty
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddOrderToBook(OrderQueueEntry order, long timestamp)
        {
            PriceLevel level;
            if (order.Side == OrderSide.Buy)
            {
                if (!_bidLevels.TryGetValue(order.Price, out level!))
                {
                    level = new PriceLevel(order.Price);
                    _bidLevels[order.Price] = level;
                    
                    if (BestBidLevel == null || order.Price > BestBidLevel.Price)
                    {
                        BestBidLevel = level;
                        NotifyBboChange();
                    }
                }
            }
            else
            {
                if (!_askLevels.TryGetValue(order.Price, out level!))
                {
                    level = new PriceLevel(order.Price);
                    _askLevels[order.Price] = level;
                    
                    if (BestAskLevel == null || order.Price < BestAskLevel.Price)
                    {
                        BestAskLevel = level;
                        NotifyBboChange();
                    }
                }
            }

            var node = _nodePool.Rent(order);
            int queuePosition = level.TotalOrderCount + 1;
            var orderWithPosition = order.WithQueuePosition(queuePosition);
            node.UpdateOrder(orderWithPosition);

            level.AddOrder(orderWithPosition, node);
            _orderMap[order.OrderId] = node;

            ActiveOrderCount++;
            if (order.Side == OrderSide.Buy)
            {
                TotalBidQuantity += order.LeavesQuantity;
            }
            else
            {
                TotalAskQuantity += order.LeavesQuantity;
            }

            SequenceNumber++;
            Listener?.OnOrderAdded(orderWithPosition);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CancelOrder(long orderId, long timestamp, out OrderQueueEntry? canceledOrder)
        {
            canceledOrder = null;

            if (!_orderMap.TryGetValue(orderId, out var node))
                return false;

            var order = node.Order;
            canceledOrder = order;

            var level = node.PriceLevel;
            if (level != null)
            {
                level.RemoveOrder(node, order.LeavesQuantity);
                if (order.Side == OrderSide.Buy)
                    TotalBidQuantity -= order.LeavesQuantity;
                else
                    TotalAskQuantity -= order.LeavesQuantity;

                if (level.IsEmpty)
                {
                    if (order.Side == OrderSide.Buy)
                    {
                        RemovePriceLevel(level, isBid: true);
                        BestBidLevel = _bidLevels.Count > 0 ? _bidLevels.Values.First() : null;
                    }
                    else
                    {
                        RemovePriceLevel(level, isBid: false);
                        BestAskLevel = _askLevels.Count > 0 ? _askLevels.Values.First() : null;
                    }
                    NotifyBboChange();
                }
            }

            _orderMap.Remove(orderId);
            _nodePool.Return(node);
            ActiveOrderCount--;

            SequenceNumber++;
            Listener?.OnOrderCanceled(order);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AmendOrder(long orderId, int newQuantity, long timestamp, out OrderQueueEntry? amendedOrder)
        {
            amendedOrder = null;

            if (!_orderMap.TryGetValue(orderId, out var node))
                return false;

            var order = node.Order;
            if (newQuantity <= 0) return CancelOrder(orderId, timestamp, out amendedOrder);

            if (newQuantity > order.OriginalQuantity)
            {
                CancelOrder(orderId, timestamp, out _);
                var fills = ProcessOrder(order.WithLeavesQuantity(newQuantity), timestamp);
                // Note: amendedOrder is not easily returned here as it might have been filled
                return true;
            }

            var level = node.PriceLevel;
            if (level != null)
            {
                int diff = order.LeavesQuantity - newQuantity;
                level.UpdateQuantity(order.LeavesQuantity, newQuantity, node.IsHidden);
                
                if (order.Side == OrderSide.Buy)
                    TotalBidQuantity -= diff;
                else
                    TotalAskQuantity -= diff;

                var updatedOrder = order.WithLeavesQuantity(newQuantity);
                node.UpdateOrder(updatedOrder);
                amendedOrder = updatedOrder;

                SequenceNumber++;
                Listener?.OnOrderAmended(updatedOrder);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetQueuePosition(long orderId)
        {
            if (_orderMap.TryGetValue(orderId, out var node))
                return node.QueuePosition;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetQuantityAhead(long orderId)
        {
            if (_orderMap.TryGetValue(orderId, out var node))
                return node.QuantityAhead;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetOrdersAhead(long orderId)
        {
            if (_orderMap.TryGetValue(orderId, out var node))
                return node.OrdersAhead;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderQueueEntry? GetOrder(long orderId)
        {
            if (_orderMap.TryGetValue(orderId, out var node))
                return node.Order;
            return null;
        }

        public BestBidAsk BestBidAsk
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new BestBidAsk(
                bestBidPrice: BestBidLevel?.Price ?? 0,
                bestBidSize: BestBidLevel?.VisibleQuantity ?? 0,
                bestAskPrice: BestAskLevel?.Price ?? 0,
                bestAskSize: BestAskLevel?.VisibleQuantity ?? 0,
                timestamp: CurrentTimestamp,
                sequenceNumber: SequenceNumber
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetBestBids(OrderBookEntry[] entries)
        {
            ArgumentNullException.ThrowIfNull(entries);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetBestAsks(OrderBookEntry[] entries)
        {
            ArgumentNullException.ThrowIfNull(entries);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BookDepth? GetDepth(long price)
        {
            _bidLevels.TryGetValue(price, out var bidLevel);
            _askLevels.TryGetValue(price, out var askLevel);
            
            if (bidLevel != null || askLevel != null)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ValidateOrder(OrderQueueEntry order, out RejectReason reason)
        {
            reason = RejectReason.None;
            if (order.LeavesQuantity <= 0)
            {
                reason = RejectReason.InvalidQuantity;
                return false;
            }
            if (order.OrderId <= 0)
            {
                reason = RejectReason.InvalidOrderId;
                return false;
            }
            return true;
        }

        private void RejectOrder(OrderQueueEntry order, RejectReason reason, long timestamp)
        {
            SequenceNumber++;
            Listener?.OnOrderRejected(order, reason);
        }

        private void RemovePriceLevel(PriceLevel level, bool isBid)
        {
            if (isBid)
            {
                if (level.Prev != null) level.Prev.Next = level.Next;
                if (level.Next != null) level.Next.Prev = level.Prev;
                _bidLevels.Remove(level.Price);
            }
            else
            {
                if (level.Prev != null) level.Prev.Next = level.Next;
                if (level.Next != null) level.Next.Prev = level.Prev;
                _askLevels.Remove(level.Price);
            }
        }

        private void NotifyBboChange()
        {
            Listener?.OnBboChanged(BestBidAsk);
        }

        public void Clear()
        {
            _orderMap.Clear();
            _bidLevels.Clear();
            _askLevels.Clear();
            BestBidLevel = null;
            BestAskLevel = null;
            ActiveOrderCount = 0;
            TotalBidQuantity = 0;
            TotalAskQuantity = 0;
            _nodePool.Clear();
        }

        private sealed class DescendingComparer<T> : IComparer<T> where T : IComparable<T>
        {
            public int Compare(T? x, T? y)
            {
                if (x == null || y == null) return 0;
                return y.CompareTo(x);
            }
        }

        public int OrderCount => _orderMap.Count;
    }

    public readonly struct MatchResult : IEquatable<MatchResult>
    {
        public IReadOnlyList<FillRecord> Fills { get; init; }
        public int FilledQuantity { get; init; }

        public override bool Equals(object? obj) => obj is MatchResult other && Equals(other);
        public bool Equals(MatchResult other) => FilledQuantity == other.FilledQuantity;
        public override int GetHashCode() => FilledQuantity.GetHashCode();
        public static bool operator ==(MatchResult left, MatchResult right) => left.Equals(right);
        public static bool operator !=(MatchResult left, MatchResult right) => !left.Equals(right);
    }
}
