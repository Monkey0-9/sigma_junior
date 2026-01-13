using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Hft.OrderBook
{
    /// <summary>
    /// Represents a price level in the order book with a queue of orders.
    /// Each price level maintains a doubly-linked list of orders for FIFO processing.
    /// 
    /// Performance: Cache-line aligned, optimized for sequential access.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 64)]
    public sealed class PriceLevel
    {
        // Hot path: Price - isolated on its own cache line
        private readonly long _pricePadding1;
        private readonly long _pricePadding2;
        private readonly long _pricePadding3;
        private readonly long _pricePadding4;
        private readonly long _pricePadding5;
        private readonly long _pricePadding6;
        private readonly long _pricePadding7;
        private readonly long _pricePadding8;

        /// <summary>Price level (in ticks)</summary>
        public long Price { get; }

        // Hot path: Aggregate quantities
        private readonly long _quantityPadding1;
        private readonly long _quantityPadding2;
        private readonly long _quantityPadding3;
        private readonly long _quantityPadding4;
        private readonly long _quantityPadding5;
        private readonly long _quantityPadding6;
        private readonly long _quantityPadding7;
        private readonly long _quantityPadding8;

        /// <summary>Total quantity at this price (visible + hidden)</summary>
        public int TotalQuantity { get; private set; }

        /// <summary>Visible quantity at this price</summary>
        public int VisibleQuantity { get; private set; }

        /// <summary>Number of visible orders at this price</summary>
        public int VisibleOrderCount { get; private set; }

        /// <summary>Number of hidden orders at this price</summary>
        public int HiddenOrderCount { get; private set; }

        public int TotalOrderCount { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => VisibleOrderCount + HiddenOrderCount; }

        // Doubly-linked list nodes for order queue (cache line aligned)
        private readonly long _listPadding1;
        private readonly long _listPadding2;
        private readonly long _listPadding3;
        private readonly long _listPadding4;
        private readonly long _listPadding5;
        private readonly long _listPadding6;
        private readonly long _listPadding7;
        private readonly long _listPadding8;

        /// <summary>First order in queue (null if empty)</summary>
        public OrderQueueNode? First { get; private set; }

        /// <summary>Last order in queue (null if empty)</summary>
        public OrderQueueNode? Last { get; private set; }

        /// <summary>Previous price level in book (for traversal)</summary>
        public PriceLevel? Prev { get; set; }

        /// <summary>Next price level in book (for traversal)</summary>
        public PriceLevel? Next { get; set; }

        // Version for optimistic concurrency
        private long _version;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PriceLevel(long price)
        {
            Price = price;
            TotalQuantity = 0;
            VisibleQuantity = 0;
            VisibleOrderCount = 0;
            HiddenOrderCount = 0;
            First = null;
            Last = null;
            Prev = null;
            Next = null;
            _version = 0;

            // Initialize padding
            _pricePadding1 = _pricePadding2 = _pricePadding3 = _pricePadding4 = 
                _pricePadding5 = _pricePadding6 = _pricePadding7 = _pricePadding8 = 0;
            _quantityPadding1 = _quantityPadding2 = _quantityPadding3 = _quantityPadding4 = 
                _quantityPadding5 = _quantityPadding6 = _quantityPadding7 = _quantityPadding8 = 0;
            _listPadding1 = _listPadding2 = _listPadding3 = _listPadding4 = 
                _listPadding5 = _listPadding6 = _listPadding7 = _listPadding8 = 0;
        }

        /// <summary>
        /// Adds an order to the back of the queue.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOrder(OrderQueueEntry order, OrderQueueNode node)
        {
            ArgumentNullException.ThrowIfNull(node);
            // Validate node
            node.PriceLevel = this;
            node.Next = null;
            node.Prev = Last;

            if (Last != null)
            {
                Last.Next = node;
            }
            else
            {
                First = node;
            }

            Last = node;

            // Update quantities
            int qty = order.LeavesQuantity;
            TotalQuantity += qty;

            if (order.IsHidden)
            {
                HiddenOrderCount++;
            }
            else
            {
                VisibleQuantity += qty;
                VisibleOrderCount++;
            }

            _version++;
        }

        /// <summary>
        /// Removes an order from the queue.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveOrder(OrderQueueNode node, int quantity)
        {
            ArgumentNullException.ThrowIfNull(node);
            if (node.Prev != null)
            {
                node.Prev.Next = node.Next;
            }
            else
            {
                First = node.Next;
            }

            if (node.Next != null)
            {
                node.Next.Prev = node.Prev;
            }
            else
            {
                Last = node.Prev;
            }

            // Update quantities
            TotalQuantity -= quantity;

            if (node.IsHidden)
            {
                HiddenOrderCount--;
            }
            else
            {
                VisibleQuantity -= quantity;
                VisibleOrderCount--;
            }

            _version++;
        }

        /// <summary>
        /// Updates order quantity (partial fill).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateQuantity(int oldQuantity, int newQuantity, bool isHidden)
        {
            int diff = newQuantity - oldQuantity;
            TotalQuantity += diff;

            if (!isHidden)
            {
                VisibleQuantity += diff;
            }

            _version++;
        }

        /// <summary>
        /// Gets the order at the front of the queue.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderQueueNode? PeekFront() => First;

        /// <summary>
        /// Returns true if the queue is empty.
        /// </summary>
        public bool IsEmpty { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => First == null; }

        /// <summary>
        /// Returns true if there are no visible orders.
        /// </summary>
        public bool HasNoVisibleOrders { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => VisibleOrderCount == 0; }

        /// <summary>
        /// Gets the current version for optimistic concurrency.
        /// </summary>
        public long Version { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Volatile.Read(ref _version); }

        /// <summary>
        /// Returns a snapshot of the L2 entry for this price level.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderBookEntry GetEntry(long sequenceNumber)
        {
            return new OrderBookEntry(
                price: Price,
                totalQuantity: TotalQuantity,
                visibleQuantity: VisibleQuantity,
                orderCount: TotalOrderCount,
                hiddenOrderCount: HiddenOrderCount,
                sequenceNumber: sequenceNumber
            );
        }
    }

    /// <summary>
    /// Node in the order queue for a price level.
    /// Doubly-linked list node for O(1) removal.
    /// 
    /// Performance: Cache line aligned (64 bytes).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 64)]
    public sealed class OrderQueueNode
    {
        // Cache line 1: Order data (hot path)
        private readonly long _dataPadding1;
        private readonly long _dataPadding2;
        private readonly long _dataPadding3;
        private readonly long _dataPadding4;
        private readonly long _dataPadding5;
        private readonly long _dataPadding6;
        private readonly long _dataPadding7;
        private readonly long _dataPadding8;

        /// <summary>Reference to the order entry</summary>
        public OrderQueueEntry Order { get; private set; }

        /// <summary>Pointer back to parent price level</summary>
        public PriceLevel? PriceLevel { get; set; }

        // Cache line 2: Linked list pointers
        private readonly long _listPadding1;
        private readonly long _listPadding2;
        private readonly long _listPadding3;
        private readonly long _listPadding4;
        private readonly long _listPadding5;
        private readonly long _listPadding6;
        private readonly long _listPadding7;
        private readonly long _listPadding8;

        /// <summary>Previous node in queue (null if first)</summary>
        public OrderQueueNode? Prev { get; set; }

        /// <summary>Next node in queue (null if last)</summary>
        public OrderQueueNode? Next { get; set; }

        // Cache line 3: Pool linkage
        private readonly long _poolPadding1;
        private readonly long _poolPadding2;
        private readonly long _poolPadding3;
        private readonly long _poolPadding4;
        private readonly long _poolPadding5;
        private readonly long _poolPadding6;
        private readonly long _poolPadding7;
        private readonly long _poolPadding8;

        /// <summary>Next node in object pool (for memory reuse)</summary>
        public OrderQueueNode? PoolNext { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderQueueNode(OrderQueueEntry order)
        {
            Order = order;
            PriceLevel = null;
            Prev = null;
            Next = null;
            PoolNext = null;

            // Initialize padding
            _dataPadding1 = _dataPadding2 = _dataPadding3 = _dataPadding4 = 
                _dataPadding5 = _dataPadding6 = _dataPadding7 = _dataPadding8 = 0;
            _listPadding1 = _listPadding2 = _listPadding3 = _listPadding4 = 
                _listPadding5 = _listPadding6 = _listPadding7 = _listPadding8 = 0;
            _poolPadding1 = _poolPadding2 = _poolPadding3 = _poolPadding4 = 
                _poolPadding5 = _poolPadding6 = _poolPadding7 = _poolPadding8 = 0;
        }

        /// <summary>
        /// Updates the order data (e.g., after partial fill).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateOrder(OrderQueueEntry newOrder)
        {
            Order = newOrder;
        }

        /// <summary>
        /// Returns true if this is a hidden order.
        /// </summary>
        public bool IsHidden { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Order.IsHidden; }

        /// <summary>
        /// Returns the current queue position (1-based from front).
        /// </summary>
        public int QueuePosition
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int pos = 1;
                var node = Prev;
                while (node != null)
                {
                    pos++;
                    node = node.Prev;
                }
                return pos;
            }
        }

        /// <summary>
        /// Returns the number of orders ahead in the queue.
        /// </summary>
        public int OrdersAhead
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int count = 0;
                var node = Prev;
                while (node != null)
                {
                    count++;
                    node = node.Prev;
                }
                return count;
            }
        }

        /// <summary>
        /// Returns the total quantity ahead in the queue (including hidden).
        /// </summary>
        public int QuantityAhead
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int qty = 0;
                var node = Prev;
                while (node != null)
                {
                    qty += node.Order.LeavesQuantity;
                    node = node.Prev;
                }
                return qty;
            }
        }
    }

    /// <summary>
    /// Object pool for order queue nodes to reduce allocations.
    /// Uses a simple lock-free stack for hot path performance.
    /// </summary>
    public sealed class OrderQueueNodePool
    {
        private OrderQueueNode? _head;
        private readonly int _maxSize;
        private int _count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderQueueNodePool(int maxSize = 1024 * 64) // 64K nodes
        {
            _maxSize = maxSize;
            _count = 0;
        }

        /// <summary>
        /// Gets a node from the pool (or creates new if empty).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderQueueNode Rent(OrderQueueEntry order)
        {
            OrderQueueNode? node = null;

            // Fast path: try to get from pool
            while (_head != null)
            {
                var head = Volatile.Read(ref _head);
                if (head == null) break;

                if (Interlocked.CompareExchange(ref _head, head.PoolNext, head) == head)
                {
                    node = head;
                    Interlocked.Decrement(ref _count);
                    break;
                }
            }

            if (node == null)
            {
                node = new OrderQueueNode(order);
            }
            else
            {
                node.UpdateOrder(order);
                node.Prev = null;
                node.Next = null;
                node.PriceLevel = null;
                node.PoolNext = null;
            }

            return node;
        }

        /// <summary>
        /// Returns a node to the pool for reuse.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(OrderQueueNode node)
        {
            ArgumentNullException.ThrowIfNull(node);
            if (_count >= _maxSize) return; // Pool full

            node.PoolNext = null;
            node.Prev = null;
            node.Next = null;
            node.PriceLevel = null;

            while (true)
            {
                var head = Volatile.Read(ref _head);
                node.PoolNext = head;

                if (Interlocked.CompareExchange(ref _head, node, head) == head)
                {
                    Interlocked.Increment(ref _count);
                    break;
                }
            }
        }

        /// <summary>
        /// Clears the pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Volatile.Write(ref _head, null);
            _count = 0;
        }

        /// <summary>
        /// Returns the current pool size.
        /// </summary>
        public int Size => _count;
    }
}

