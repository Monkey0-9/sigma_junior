using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Hft.Core;
using Hft.OrderBook;

namespace Hft.Routing
{
    /// <summary>
    /// Pluggable venue adapter interface for multi-venue execution.
    /// 
    /// Design Principles:
    /// - Abstract away venue-specific protocols (FIX, binary, REST, etc.)
    /// - Provide uniform interface for routing decisions
    /// - Enable testing with mock venues
    /// - Support both lit and dark venue types
    /// 
    /// Thread Safety: All methods must be thread-safe for concurrent routing.
    /// </summary>
    public interface IVenueAdapter : IDisposable
    {
        /// <summary>
        /// Unique venue identifier.
        /// </summary>
        string VenueId { get; }

        /// <summary>
        /// Human-readable venue name.
        /// </summary>
        string VenueName { get; }

        /// <summary>
        /// Venue type (exchange, dark pool, ATS, etc.)
        /// </summary>
        VenueType VenueType { get; }

        /// <summary>
        /// Whether the venue is currently available for trading.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Gets the current status of the venue.
        /// </summary>
        VenueStatus GetStatus();

        /// <summary>
        /// Sends an order to the venue.
        /// </summary>
        /// <param name="order">The order to send</param>
        /// <returns>Order acknowledgment (with exchange order ID if available)</returns>
        OrderAck SendOrder(OrderQueueEntry order);

        /// <summary>
        /// Cancels an order at the venue.
        /// </summary>
        /// <param name="orderId">The local order ID</param>
        /// <returns>True if cancel was acknowledged</returns>
        bool CancelOrder(long orderId);

        /// <summary>
        /// Amends an order at the venue.
        /// </summary>
        /// <param name="orderId">The local order ID</param>
        /// <param name="newQuantity">The new quantity</param>
        /// <returns>True if amend was acknowledged</returns>
        bool AmendOrder(long orderId, int newQuantity);

        /// <summary>
        /// Gets the current order status from the venue.
        /// </summary>
        /// <param name="orderId">The local order ID</param>
        /// <returns>Current order status, or null if not found</returns>
        OrderStatus? GetOrderStatus(long orderId);

        /// <summary>
        /// Gets the current order book snapshot from the venue.
        /// </summary>
        /// <param name="instrumentId">The instrument ID</param>
        /// <returns>Order book snapshot, or null if not available</returns>
        VenueOrderBookSnapshot? GetOrderBook(long instrumentId);

        /// <summary>
        /// Gets the current best bid/ask from the venue.
        /// </summary>
        /// <param name="instrumentId">The instrument ID</param>
        /// <returns>BBO snapshot, or null if not available</returns>
        VenueBboSnapshot? GetBestBidAsk(long instrumentId);

        /// <summary>
        /// Estimates the fill probability for a hypothetical order.
        /// </summary>
        /// <param name="side">Order side</param>
        /// <param name="price">Limit price</param>
        /// <param name="quantity">Order quantity</param>
        /// <param name="orderType">Order type</param>
        /// <returns>Estimated fill probability (0-1)</returns>
        double EstimateFillProbability(OrderSide side, long price, int quantity, OrderType orderType);

        /// <summary>
        /// Gets the current latency estimate to this venue.
        /// </summary>
        /// <returns>Latency in microseconds</returns>
        double GetLatencyMicroseconds();

        /// <summary>
        /// Gets the fee schedule for this venue.
        /// </summary>
        /// <param name="instrumentId">The instrument ID</param>
        /// <returns>Fee schedule</returns>
        VenueFeeSchedule GetFeeSchedule(long instrumentId);

        /// <summary>
        /// Checks if an order would be accepted by the venue.
        /// </summary>
        /// <param name="order">The order to check</param>
        /// <returns>Validation result</returns>
        VenueOrderValidation ValidateOrder(OrderQueueEntry order);

        /// <summary>
        /// Gets the current rate limit status.
        /// </summary>
        RateLimitStatus GetRateLimitStatus();

        /// <summary>
        /// Gets historical venue performance metrics.
        /// </summary>
        VenuePerformanceMetrics GetPerformanceMetrics();
    }

    /// <summary>
    /// Type of execution venue.
    /// </summary>
    public enum VenueType
    {
        /// <summary>Public exchange (lit)</summary>
        Exchange = 0,

        /// <summary>Alternative Trading System</summary>
        ATS = 1,

        /// <summary>Dark pool</summary>
        DarkPool = 2,

        /// <summary>Systematic internalizer</summary>
        SystematicInternalizer = 3,

        /// <summary>Broker's smart router</summary>
        SmartRouter = 4,

        /// <summary>Crossing network</summary>
        CrossingNetwork = 5
    }

    /// <summary>
    /// Venue operational status.
    /// </summary>
    public enum VenueStatus
    {
        /// <summary>Venue is operating normally</summary>
        Operational = 0,

        /// <summary>Venue is in pre-open state</summary>
        PreOpen = 1,

        /// <summary>Venue is in auction phase</summary>
        Auction = 2,

        /// <summary>Venue is trading normally</summary>
        Trading = 3,

        /// <summary>Venue is in closing auction</summary>
        Closing = 4,

        /// <summary>Venue is halt/trading pause</summary>
        Halted = 5,

        /// <summary>Venue is closed</summary>
        Closed = 6,

        /// <summary>Venue is unavailable</summary>
        Unavailable = 7,

        /// <summary>Venue has rejected connection</summary>
        Rejected = 8
    }

    /// <summary>
    /// Order acknowledgment from venue.
    /// </summary>
    public readonly struct OrderAck
    {
        /// <summary>Local order ID</summary>
        public long LocalOrderId { get; init; }

        /// <summary>Exchange-assigned order ID (if available)</summary>
        public string? ExchangeOrderId { get; init; }

        /// <summary>Whether the order was accepted</summary>
        public bool IsAccepted { get; init; }

        /// <summary>Reject reason if not accepted</summary>
        public string? RejectReason { get; init; }

        /// <summary>Queue position (if available)</summary>
        public int? QueuePosition { get; init; }

        /// <summary>Timestamp of acknowledgment</summary>
        public long AckTimestamp { get; init; }

        /// <summary>Latency from send to ack (microseconds)</summary>
        public double LatencyMicroseconds { get; init; }

        /// <summary>
        /// Creates an accepted order acknowledgment.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OrderAck Accepted(
            long localOrderId,
            string? exchangeOrderId = null,
            int? queuePosition = null,
            double latencyUs = 0)
        {
            return new OrderAck
            {
                LocalOrderId = localOrderId,
                ExchangeOrderId = exchangeOrderId,
                IsAccepted = true,
                QueuePosition = queuePosition,
                LatencyMicroseconds = latencyUs,
                AckTimestamp = Stopwatch.GetTimestamp()
            };
        }

        /// <summary>
        /// Creates a rejected order acknowledgment.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OrderAck Rejected(long localOrderId, string reason, double latencyUs = 0)
        {
            return new OrderAck
            {
                LocalOrderId = localOrderId,
                IsAccepted = false,
                RejectReason = reason,
                LatencyMicroseconds = latencyUs,
                AckTimestamp = Stopwatch.GetTimestamp()
            };
        }
    }

    /// <summary>
    /// Order book snapshot from a venue.
    /// </summary>
    public readonly struct VenueOrderBookSnapshot
    {
        /// <summary>Instrument ID</summary>
        public long InstrumentId { get; init; }

        /// <summary>Snapshot timestamp</summary>
        public long Timestamp { get; init; }

        /// <summary>Sequence number (if available)</summary>
        public long? SequenceNumber { get; init; }

        /// <summary>Bid levels (price, size, order count)</summary>
        public IReadOnlyList<VenuePriceLevel> Bids { get; init; }

        /// <summary>Ask levels (price, size, order count)</summary>
        public IReadOnlyList<VenuePriceLevel> Asks { get; init; }
    }

    /// <summary>
    /// Price level from a venue.
    /// </summary>
    public readonly struct VenuePriceLevel
    {
        /// <summary>Price</summary>
        public long Price { get; init; }

        /// <summary>Total size at this level</summary>
        public int Size { get; init; }

        /// <summary>Number of orders at this level</summary>
        public int OrderCount { get; init; }

        /// <summary>Is this a hidden level?</summary>
        public bool IsHidden { get; init; }
    }

    /// <summary>
    /// Best bid/ask snapshot from a venue.
    /// </summary>
    public readonly struct VenueBboSnapshot
    {
        /// <summary>Instrument ID</summary>
        public long InstrumentId { get; init; }

        /// <summary>Snapshot timestamp</summary>
        public long Timestamp { get; init; }

        /// <summary>Best bid price</summary>
        public long BestBidPrice { get; init; }

        /// <summary>Best bid size</summary>
        public int BestBidSize { get; init; }

        /// <summary>Best ask price</summary>
        public long BestAskPrice { get; init; }

        /// <summary>Best ask size</summary>
        public int BestAskSize { get; init; }

        /// <summary>Mid price</summary>
        public double MidPrice => (BestBidPrice + BestAskPrice) / 2.0;

        /// <summary>Spread (bps)</summary>
        public double SpreadBps => BestAskPrice > BestBidPrice 
            ? ((BestAskPrice - BestBidPrice) / MidPrice) * 10000 : 0;
    }

    /// <summary>
    /// Venue fee schedule.
    /// </summary>
    public readonly struct VenueFeeSchedule
    {
        /// <summary>Instrument ID</summary>
        public long InstrumentId { get; init; }

        /// <summary>Maker fee (bps, negative = rebate)</summary>
        public double MakerFeeBps { get; init; }

        /// <summary>Taker fee (bps)</summary>
        public double TakerFeeBps { get; init; }

        /// <summary>Minimum fee (in currency units)</summary>
        public double MinimumFee { get; init; }

        /// <summary>Rebate for providing liquidity (bps)</summary>
        public double LiquidityRebateBps { get; init; }

        /// <summary>Regulatory fees (bps)</summary>
        public double RegulatoryFeeBps { get; init; }

        /// <summary>Whether maker rebates are passed through</summary>
        public bool PassThroughRebates { get; init; }

        /// <summary>
        /// Calculates net fee for a trade.
        /// </summary>
        /// <param name="quantity">Trade quantity</param>
        /// <param name="price">Execution price</param>
        /// <param name="isMaker">Whether this was a maker (passive) trade</param>
        /// <returns>Net fee in currency units</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double CalculateNetFee(int quantity, double price, bool isMaker)
        {
            double notional = quantity * price;
            double baseFee = notional * (isMaker ? MakerFeeBps : TakerFeeBps) / 10000.0;
            double rebate = isMaker ? notional * LiquidityRebateBps / 10000.0 : 0;
            double netFee = baseFee - rebate;
            return Math.Max(netFee, MinimumFee);
        }
    }

    /// <summary>
    /// Order validation result from venue.
    /// </summary>
    public readonly struct VenueOrderValidation
    {
        /// <summary>Whether the order would be accepted</summary>
        public bool IsValid { get; init; }

        /// <summary>Reject reason if invalid</summary>
        public string? RejectReason { get; init; }

        /// <summary>Any warnings (order still accepted)</summary>
        public IReadOnlyList<string> Warnings { get; init; }

        /// <summary>Maximum quantity allowed</summary>
        public int? MaxQuantity { get; init; }

        /// <summary>Minimum quantity allowed</summary>
        public int? MinQuantity { get; init; }

        /// <summary>Tick size restriction</summary>
        public long? TickSize { get; init; }

        /// <summary>
        /// Creates a valid validation result.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VenueOrderValidation Valid()
        {
            return new VenueOrderValidation { IsValid = true };
        }

        /// <summary>
        /// Creates an invalid validation result.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VenueOrderValidation Invalid(string reason)
        {
            return new VenueOrderValidation { IsValid = false, RejectReason = reason };
        }
    }

    /// <summary>
    /// Rate limit status at a venue.
    /// </summary>
    public readonly struct RateLimitStatus
    {
        /// <summary>Current orders per second</summary>
        public double OrdersPerSecond { get; init; }

        /// <summary>Maximum orders per second</summary>
        public double MaxOrdersPerSecond { get; init; }

        /// <summary>Orders remaining in window</summary>
        public int OrdersRemaining { get; init; }

        /// <summary>Current messages per second</summary>
        public double MessagesPerSecond { get; init; }

        /// <summary>Maximum messages per second</summary>
        public double MaxMessagesPerSecond { get; init; }

        /// <summary>Current notional per second</summary>
        public double NotionalPerSecond { get; init; }

        /// <summary>Maximum notional per second</summary>
        public double MaxNotionalPerSecond { get; init; }

        /// <summary>Whether rate limited</summary>
        public bool IsRateLimited => OrdersRemaining <= 0;

        /// <summary>Time until reset (microseconds)</summary>
        public long ResetInMicroseconds { get; init; }
    }

    /// <summary>
    /// Historical performance metrics for a venue.
    /// </summary>
    public readonly struct VenuePerformanceMetrics
    {
        /// <summary>Venue ID</summary>
        public string VenueId { get; init; }

        /// <summary>Measurement period start</summary>
        public long PeriodStart { get; init; }

        /// <summary>Measurement period end</summary>
        public long PeriodEnd { get; init; }

        /// <summary>Total orders sent</summary>
        public long TotalOrdersSent { get; init; }

        /// <summary>Total orders filled</summary>
        public long TotalOrdersFilled { get; init; }

        /// <summary>Total quantity filled</summary>
        public long TotalQuantityFilled { get; init; }

        /// <summary>Fill rate</summary>
        public double FillRate => TotalOrdersSent > 0 
            ? (double)TotalOrdersFilled / TotalOrdersSent : 0;

        /// <summary>Average execution latency (microseconds)</summary>
        public double AvgLatencyUs { get; init; }

        /// <summary>P50 latency</summary>
        public double P50LatencyUs { get; init; }

        /// <summary>P99 latency</summary>
        public double P99LatencyUs { get; init; }

        /// <summary>Average implementation shortfall (bps)</summary>
        public double AvgImplementationShortfallBps { get; init; }

        /// <summary>Average effective spread (bps)</summary>
        public double AvgEffectiveSpreadBps { get; init; }

        /// <summary>Rejection rate</summary>
        public double RejectionRate { get; init; }

        /// <summary>Cancel rate</summary>
        public double CancelRate { get; init; }
    }

    /// <summary>
    /// Event arguments for order status updates from a venue.
    /// </summary>
    public class OrderStatusUpdateEventArgs : EventArgs
    {
        /// <summary>Local order ID</summary>
        public long OrderId { get; init; }

        /// <summary>New status</summary>
        public OrderStatus Status { get; init; }

        /// <summary>Leaves quantity</summary>
        public int LeavesQuantity { get; init; }

        /// <summary>Cumulative filled quantity</summary>
        public int FilledQuantity { get; init; }

        /// <summary>Average filled price</summary>
        public double AvgPrice { get; init; }

        /// <summary>Timestamp</summary>
        public long Timestamp { get; init; }

        /// <summary>Reject reason if rejected</summary>
        public string? RejectReason { get; init; }
    }

    /// <summary>
    /// Event arguments for fills from a venue.
    /// </summary>
    public class FillEventArgs : EventArgs
    {
        /// <summary>Local order ID</summary>
        public long OrderId { get; init; }

        /// <summary>Fill quantity</summary>
        public int Quantity { get; init; }

        /// <summary>Fill price</summary>
        public double Price { get; init; }

        /// <summary>Is this a hidden fill?</summary>
        public bool IsHidden { get; init; }

        /// <summary>Is this a liquidity maker fill?</summary>
        public bool IsMaker { get; init; }

        /// <summary>Timestamp</summary>
        public long Timestamp { get; init; }

        /// <summary>Exchange trade ID</summary>
        public string? ExchangeTradeId { get; init; }
    }
}

