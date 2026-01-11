using System;

namespace Hft.OrderBook
{
    /// <summary>
    /// Interface for order book event listeners.
    /// Implement this to receive callbacks for order book events.
    /// 
    /// Performance: All methods are virtual - override only what you need.
    /// </summary>
    public interface IOrderBookListener
    {
        /// <summary>
        /// Called when a trade is executed.
        /// </summary>
        void OnTrade(FillRecord fill);

        /// <summary>
        /// Called when an order is added to the book.
        /// </summary>
        void OnOrderAdded(OrderQueueEntry order);

        /// <summary>
        /// Called when an order is canceled.
        /// </summary>
        void OnOrderCanceled(OrderQueueEntry order);

        /// <summary>
        /// Called when an order is amended.
        /// </summary>
        void OnOrderAmended(OrderQueueEntry order);

        /// <summary>
        /// Called when an order is rejected.
        /// </summary>
        void OnOrderRejected(OrderQueueEntry order, RejectReason reason);

        /// <summary>
        /// Called when the best bid/ask changes.
        /// </summary>
        void OnBboChanged(BestBidAsk bbo);
    }

    /// <summary>
    /// Default no-op listener for scenarios where not all events are needed.
    /// </summary>
    public sealed class NoOpOrderBookListener : IOrderBookListener
    {
        public static readonly NoOpOrderBookListener Instance = new();

        public void OnTrade(FillRecord fill) { }
        public void OnOrderAdded(OrderQueueEntry order) { }
        public void OnOrderCanceled(OrderQueueEntry order) { }
        public void OnOrderAmended(OrderQueueEntry order) { }
        public void OnOrderRejected(OrderQueueEntry order, RejectReason reason) { }
        public void OnBboChanged(BestBidAsk bbo) { }
    }

    /// <summary>
    /// Buffering listener that accumulates events and dispatches in batches.
    /// Useful for high-throughput scenarios where you want to reduce callback overhead.
    /// </summary>
    public class BufferedOrderBookListener : IOrderBookListener
    {
        private readonly IOrderBookListener _inner;
        private readonly int _batchSize;
        private readonly System.Collections.Generic.List<FillRecord> _tradeBuffer = new();
        private readonly System.Collections.Generic.List<OrderQueueEntry> _orderBuffer = new();

        public BufferedOrderBookListener(IOrderBookListener inner, int batchSize = 100)
        {
            _inner = inner;
            _batchSize = batchSize;
        }

        public void OnTrade(FillRecord fill)
        {
            _tradeBuffer.Add(fill);
            if (_tradeBuffer.Count >= _batchSize)
                FlushTrades();
        }

        public void OnOrderAdded(OrderQueueEntry order)
        {
            _orderBuffer.Add(order);
            if (_orderBuffer.Count >= _batchSize)
                FlushOrders();
        }

        public void OnOrderCanceled(OrderQueueEntry order)
        {
            _orderBuffer.Add(order);
            if (_orderBuffer.Count >= _batchSize)
                FlushOrders();
        }

        public void OnOrderAmended(OrderQueueEntry order)
        {
            _orderBuffer.Add(order);
            if (_orderBuffer.Count >= _batchSize)
                FlushOrders();
        }

        public void OnOrderRejected(OrderQueueEntry order, RejectReason reason)
        {
            _orderBuffer.Add(order);
            if (_orderBuffer.Count >= _batchSize)
                FlushOrders();
        }

        public void OnBboChanged(BestBidAsk bbo)
        {
            // BBO changes are not buffered - forward immediately
            _inner.OnBboChanged(bbo);
        }

        /// <summary>
        /// Flushes all buffered events.
        /// Call this before destroying the listener.
        /// </summary>
        public void Flush()
        {
            FlushTrades();
            FlushOrders();
        }

        private void FlushTrades()
        {
            foreach (var fill in _tradeBuffer)
                _inner.OnTrade(fill);
            _tradeBuffer.Clear();
        }

        private void FlushOrders()
        {
            foreach (var order in _orderBuffer)
            {
                // Determine the event type from context - simplified here
                if (order.Status == OrderStatus.Active)
                    _inner.OnOrderAdded(order);
                else if (order.Status == OrderStatus.Canceled)
                    _inner.OnOrderCanceled(order);
                else if (order.Status == OrderStatus.Filled)
                    _inner.OnOrderAdded(order); // Was added before fill
            }
            _orderBuffer.Clear();
        }
    }

    /// <summary>
    /// Statistics listener that aggregates order book metrics.
    /// </summary>
    public class StatisticsOrderBookListener : IOrderBookListener
    {
        // Trade statistics
        public long TotalTrades { get; private set; }
        public long TotalVolume { get; private set; }
        public long TotalNotional { get; private set; }
        public double AverageTradeSize { get; private set; }

        // Order statistics
        public long TotalOrdersAdded { get; private set; }
        public long TotalOrdersCanceled { get; private set; }
        public long TotalOrdersAmended { get; private set; }
        public long TotalOrdersRejected { get; private set; }

        // BBO changes
        public long TotalBboChanges { get; private set; }

        // Latency tracking
        public long FirstTimestamp { get; private set; }
        public long LastTimestamp { get; private set; }

        public void OnTrade(FillRecord fill)
        {
            TotalTrades++;
            TotalVolume += fill.Quantity;
            TotalNotional += fill.Notional;
            
            if (TotalTrades == 1)
                FirstTimestamp = fill.Timestamp;
            LastTimestamp = fill.Timestamp;

            AverageTradeSize = (double)TotalVolume / TotalTrades;
        }

        public void OnOrderAdded(OrderQueueEntry order)
        {
            TotalOrdersAdded++;
        }

        public void OnOrderCanceled(OrderQueueEntry order)
        {
            TotalOrdersCanceled++;
        }

        public void OnOrderAmended(OrderQueueEntry order)
        {
            TotalOrdersAmended++;
        }

        public void OnOrderRejected(OrderQueueEntry order, RejectReason reason)
        {
            TotalOrdersRejected++;
        }

        public void OnBboChanged(BestBidAsk bbo)
        {
            TotalBboChanges++;
        }

        /// <summary>
        /// Resets all statistics.
        /// </summary>
        public void Reset()
        {
            TotalTrades = 0;
            TotalVolume = 0;
            TotalNotional = 0;
            AverageTradeSize = 0;
            TotalOrdersAdded = 0;
            TotalOrdersCanceled = 0;
            TotalOrdersAmended = 0;
            TotalOrdersRejected = 0;
            TotalBboChanges = 0;
            FirstTimestamp = 0;
            LastTimestamp = 0;
        }

        /// <summary>
        /// Returns a summary string.
        /// </summary>
        public string GetSummary()
        {
            double durationSeconds = LastTimestamp > FirstTimestamp 
                ? (LastTimestamp - FirstTimestamp) / 1_000_000.0 
                : 0;

            return $"Trades: {TotalTrades:N0}, Volume: {TotalVolume:N0}, " +
                   $"Notional: {TotalNotional:N2}, Orders: +{TotalOrdersAdded:N0}/-{TotalOrdersCanceled:N0}, " +
                   $"Duration: {durationSeconds:F3}s, " +
                   $"Throughput: {(durationSeconds > 0 ? TotalTrades / durationDelta : 0):N0} trades/sec";
        }
    }
}

