using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Hft.Core;
using Hft.OrderBook;

namespace Hft.Routing
{
    /// <summary>
    /// Smart Order Router for institutional multi-venue execution.
    /// 
    /// Design Principles:
    /// - Risk-first: All routing decisions pass through pre-trade risk gating
    /// - Deterministic evaluation: Same inputs produce same outputs
    /// - Auditability: Every route decision logged with full context
    /// - Interoperability: Pluggable venue adapters and throttles
    /// 
    /// Routing Algorithm:
    /// Maximizes Expected Utility = FillProbability × ExpectedPriceImprovement − Cost
    /// 
    /// Features:
    /// - Queue position awareness for passive vs aggressive orders
    /// - Dynamic rebalancing across venues
    /// - POV/TWAP/VWAP slicing strategies
    /// - Real-time venue performance adaptation
    /// 
    /// Performance: Target <100μs decision latency per routing cycle
    /// </summary>
    public interface ISmartOrderRouter : IDisposable
    {
        /// <summary>
        /// Gets the router's current status.
        /// </summary>
        RouterStatus Status { get; }

        /// <summary>
        /// Registers a venue adapter with the router.
        /// </summary>
        void RegisterVenue(IVenueAdapter venue);

        /// <summary>
        /// Unregisters a venue adapter.
        /// </summary>
        void UnregisterVenue(string venueId);

        /// <summary>
        /// Gets all registered venues.
        /// </summary>
        IReadOnlyList<IVenueAdapter> GetRegisteredVenues();

        /// <summary>
        /// Computes the optimal routing plan for a parent order.
        /// </summary>
        /// <param name="parentOrder">The parent order to route</param>
        /// <param name="liquiditySnapshot">Current market liquidity across venues</param>
        /// <returns>Routing plan with child orders per venue</returns>
        RoutingPlan ComputeRoutingPlan(ParentOrder parentOrder, LiquiditySnapshot liquiditySnapshot);

        /// <summary>
        /// Executes a routing plan and monitors execution.
        /// </summary>
        /// <param name="plan">The routing plan to execute</param>
        /// <returns>Execution result with all fills and metrics</returns>
        ExecutionResult ExecutePlan(RoutingPlan plan);

        /// <summary>
        /// Cancels all child orders for a parent order.
        /// </summary>
        /// <param name="parentOrderId">The parent order ID</param>
        void CancelAllChildren(long parentOrderId);

        /// <summary>
        /// Gets the current routing statistics.
        /// </summary>
        RouterStatistics GetStatistics();

        /// <summary>
        /// Updates the routing strategy parameters at runtime.
        /// </summary>
        void UpdateStrategyParameters(RoutingStrategyParameters parameters);
    }

    /// <summary>
    /// Router operational status.
    /// </summary>
    public enum RouterStatus
    {
        /// <summary>Router is initializing</summary>
        Initializing = 0,

        /// <summary>Router is operational</summary>
        Operational = 1,

        /// <summary>Router is degraded (some venues unavailable)</summary>
        Degraded = 2,

        /// <summary>Router is paused (emergency stop)</summary>
        Paused = 3,

        /// <summary>Router has encountered a fatal error</summary>
        Error = 4
    }

    /// <summary>
    /// Parent order representing the aggregate order to be routed.
    /// </summary>
    public readonly struct ParentOrder
    {
        /// <summary>Unique parent order ID</summary>
        public long ParentOrderId { get; init; }

        /// <summary>Instrument/symbol ID</summary>
        public long InstrumentId { get; init; }

        /// <summary>Order side (Buy/Sell)</summary>
        public OrderSide Side { get; init; }

        /// <summary>Total quantity to execute</summary>
        public int TotalQuantity { get; init; }

        /// <summary>Limit price (null for market orders)</summary>
        public double? LimitPrice { get; init; }

        /// <summary>Order intent (aggression level)</summary>
        public OrderIntent Intent { get; init; }

        /// <summary>Time-in-force for the parent order</summary>
        public TimeInForce TimeInForce { get; init; }

        /// <summary>Execution strategy (POV, TWAP, VWAP, etc.)</summary>
        public ExecutionStrategy Strategy { get; init; }

        /// <summary>Strategy-specific parameters (POV percentage, TWAP intervals, etc.)</summary>
        public StrategyParameters StrategyParameters { get; init; }

        /// <summary>Submission timestamp (microseconds)</summary>
        public long SubmissionTimestamp { get; init; }

        /// <summary>Client-provided order ID</summary>
        public string? ClientOrderId { get; init; }

        /// <summary>
        /// Creates a parent order with default parameters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ParentOrder Create(
            long parentOrderId,
            long instrumentId,
            OrderSide side,
            int quantity,
            double? limitPrice = null,
            OrderIntent intent = OrderIntent.Normal,
            TimeInForce tif = TimeInForce.Day,
            ExecutionStrategy strategy = ExecutionStrategy.POV,
            StrategyParameters? strategyParams = null)
        {
            return new ParentOrder
            {
                ParentOrderId = parentOrderId,
                InstrumentId = instrumentId,
                Side = side,
                TotalQuantity = quantity,
                LimitPrice = limitPrice,
                Intent = intent,
                TimeInForce = tif,
                Strategy = strategy,
                StrategyParameters = strategyParams ?? new StrategyParameters(),
                SubmissionTimestamp = Stopwatch.GetTimestamp(),
                ClientOrderId = null
            };
        }
    }

    /// <summary>
    /// Order intent/aggression level for routing decisions.
    /// </summary>
    public enum OrderIntent
    {
        /// <summary>Passive: prefer posting to queue, willing to wait</summary>
        Passive = 0,

        /// <summary>Normal: balanced approach between passive and aggressive</summary>
        Normal = 1,

        /// <summary>Urgent: prefer immediate execution, willing to pay spread</summary>
        Urgent = 2,

        /// <summary>Aggressive: sweep book, accept worst price if necessary</summary>
        Aggressive = 3,

        /// <summary>Emergency: must execute immediately at any price</summary>
        Emergency = 4
    }

    /// <summary>
    /// Execution strategy types.
    /// </summary>
    public enum ExecutionStrategy
    {
        /// <summary>Percentage of Volume: execute at fixed % of market volume</summary>
        POV = 0,

        /// <summary>Time Weighted Average Price: evenly distribute over time</summary>
        TWAP = 1,

        /// <summary>Volume Weighted Average Price: target VWAP</summary>
        VWAP = 2,

        /// <summary>Implementation Shortfall: optimize for total cost</summary>
        ImplementationShortfall = 3,

        /// <summary>Immediate Or Cancel: must fill immediately or cancel</summary>
        IOC = 4,

        /// <summary>All Or Nothing: must fill completely or nothing</summary>
        AON = 5
    }

    /// <summary>
    /// Strategy-specific parameters for execution algorithms.
    /// </summary>
    public readonly struct StrategyParameters
    {
        /// <summary>POV percentage (0.01 = 1%)</summary>
        public double PovPercentage { get; init; }

        /// <summary>TWAP interval count (number of slices)</summary>
        public int TwapIntervalCount { get; init; }

        /// <summary>VWAP participation limit</summary>
        public double VwapParticipationLimit { get; init; }

        /// <summary>Maximum slice size</summary>
        public int MaxSliceSize { get; init; }

        /// <summary>Minimum slice size</summary>
        public int MinSliceSize { get; init; }

        /// <summary>Aggression override (0-1, where 1 is most aggressive)</summary>
        public double AggressionOverride { get; init; }

        /// <summary>Whether to use hidden orders</summary>
        public bool UseHiddenOrders { get; init; }

        /// <summary>Whether to use iceberg orders</summary>
        public bool UseIcebergOrders { get; init; }

        /// <summary>Iceberg display quantity (if UseIcebergOrders is true)</summary>
        public int IcebergDisplayQuantity { get; init; }

        /// <summary>
        /// Creates default strategy parameters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StrategyParameters Default()
        {
            return new StrategyParameters
            {
                PovPercentage = 0.10, // 10% POV
                TwapIntervalCount = 20, // 20 slices
                VwapParticipationLimit = 0.15, // 15% max participation
                MaxSliceSize = 0, // Auto-calculate
                MinSliceSize = 100,
                AggressionOverride = 0.5,
                UseHiddenOrders = false,
                UseIcebergOrders = false,
                IcebergDisplayQuantity = 0
            };
        }
    }

    /// <summary>
    /// Routing plan specifying how to distribute a parent order across venues.
    /// </summary>
    public readonly struct RoutingPlan
    {
        /// <summary>Parent order ID this plan is for</summary>
        public long ParentOrderId { get; init; }

        /// <summary>Child orders per venue</summary>
        public IReadOnlyList<ChildOrderSpec> ChildOrders { get; init; }

        /// <summary>Total quantity allocated</summary>
        public int TotalAllocatedQuantity { get; init; }

        /// <summary>Expected fill probability weighted average</summary>
        public double ExpectedFillProbability { get; init; }

        /// <summary>Expected total cost (in basis points)</summary>
        public double ExpectedCostBps { get; init; }

        /// <summary>Routing decision timestamp</summary>
        public long DecisionTimestamp { get; init; }

        /// <summary>Plan validity duration (microseconds)</summary>
        public long PlanValidityMicroseconds { get; init; }

        /// <summary>Routing decision ID for audit</summary>
        public Guid DecisionId { get; init; }

        /// <summary>
        /// Creates a routing plan.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RoutingPlan Create(
            long parentOrderId,
            IReadOnlyList<ChildOrderSpec> childOrders,
            double expectedFillProb,
            double expectedCostBps,
            long validityMicroseconds = 1_000_000) // 1 second default
        {
            int totalQty = 0;
            foreach (var child in childOrders)
            {
                totalQty += child.Quantity;
            }

            return new RoutingPlan
            {
                ParentOrderId = parentOrderId,
                ChildOrders = childOrders,
                TotalAllocatedQuantity = totalQty,
                ExpectedFillProbability = expectedFillProb,
                ExpectedCostBps = expectedCostBps,
                DecisionTimestamp = Stopwatch.GetTimestamp(),
                PlanValidityMicroseconds = validityMicroseconds,
                DecisionId = Guid.NewGuid()
            };
        }
    }

    /// <summary>
    /// Child order specification for a specific venue.
    /// </summary>
    public readonly struct ChildOrderSpec
    {
        /// <summary>Venue ID to route to</summary>
        public string VenueId { get; init; }

        /// <summary>Order type at this venue</summary>
        public OrderType OrderType { get; init; }

        /// <summary>Quantity to send to this venue</summary>
        public int Quantity { get; init; }

        /// <summary>Limit price (if applicable)</summary>
        public double LimitPrice { get; init; }

        /// <summary>Time-in-force for this child order</summary>
        public TimeInForce TimeInForce { get; init; }

        /// <summary>Order flags (hidden, post-only, etc.)</summary>
        public OrderFlags Flags { get; init; }

        /// <summary>Expected fill probability at this venue</summary>
        public double FillProbability { get; init; }

        /// <summary>Expected execution price improvement (bps)</summary>
        public double ExpectedPriceImprovementBps { get; init; }

        /// <summary>Expected fees (bps, negative = rebate)</summary>
        public double ExpectedFeesBps { get; init; }

        /// <summary>Latency to venue (microseconds)</summary>
        public double LatencyMicroseconds { get; init; }

        /// <summary>
        /// Creates a child order specification.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChildOrderSpec Create(
            string venueId,
            OrderType orderType,
            int quantity,
            double? limitPrice = null,
            TimeInForce tif = TimeInForce.Day,
            OrderFlags flags = OrderFlags.None,
            double fillProbability = 1.0,
            double priceImprovementBps = 0,
            double feesBps = 0,
            double latencyUs = 0)
        {
            return new ChildOrderSpec
            {
                VenueId = venueId,
                OrderType = orderType,
                Quantity = quantity,
                LimitPrice = limitPrice ?? 0,
                TimeInForce = tif,
                Flags = flags,
                FillProbability = fillProbability,
                ExpectedPriceImprovementBps = priceImprovementBps,
                ExpectedFeesBps = feesBps,
                LatencyMicroseconds = latencyUs
            };
        }
    }

    /// <summary>
    /// Execution result after completing a routing plan.
    /// </summary>
    public readonly struct ExecutionResult
    {
        /// <summary>Parent order ID</summary>
        public long ParentOrderId { get; init; }

        /// <summary>Total quantity filled</summary>
        public int FilledQuantity { get; init; }

        /// <summary>Remaining quantity (unfilled)</summary>
        public int RemainingQuantity { get; init; }

        /// <summary>Weighted average fill price</summary>
        public double AverageFillPrice { get; init; }

        /// <summary>Implementation shortfall (bps)</summary>
        public double ImplementationShortfallBps { get; init; }

        /// <summary>Effective spread (bps)</summary>
        public double EffectiveSpreadBps { get; init; }

        /// <summary>Total fees paid (bps)</summary>
        public double TotalFeesBps { get; init; }

        /// <summary>Total rebates received (bps)</summary>
        public double TotalRebatesBps { get; init; }

        /// <summary>Execution duration (microseconds)</summary>
        public long ExecutionDurationMicroseconds { get; init; }

        /// <summary>Fill details per venue</summary>
        public IReadOnlyList<VenueExecutionResult> VenueResults { get; init; }

        /// <summary>Whether the execution completed fully</summary>
        public bool IsComplete => RemainingQuantity == 0;

        /// <summary>Whether the execution was successful (filled >= target)</summary>
        public bool IsSuccessful => FilledQuantity > 0;
    }

    /// <summary>
    /// Execution result from a single venue.
    /// </summary>
    public readonly struct VenueExecutionResult
    {
        /// <summary>Venue ID</summary>
        public string VenueId { get; init; }

        /// <summary>Quantity sent to venue</summary>
        public int SentQuantity { get; init; }

        /// <summary>Quantity filled at venue</summary>
        public int FilledQuantity { get; init; }

        /// <summary>Fill rate at venue</summary>
        public double FillRate => SentQuantity > 0 ? (double)FilledQuantity / SentQuantity : 0;

        /// <summary>Average fill price at venue</summary>
        public double AverageFillPrice { get; init; }

        /// <summary>Number of fills at venue</summary>
        public int FillCount { get; init; }

        /// <summary>Latency to venue (microseconds)</summary>
        public double LatencyMicroseconds { get; init; }

        /// <summary>Any rejections from venue</summary>
        public int RejectionCount { get; init; }
    }

    /// <summary>
    /// Current market liquidity across all venues.
    /// </summary>
    public readonly struct LiquiditySnapshot
    {
        /// <summary>Instrument ID</summary>
        public long InstrumentId { get; init; }

        /// <summary>Snapshot timestamp</summary>
        public long Timestamp { get; init; }

        /// <summary>Liquidity per venue</summary>
        public IReadOnlyList<VenueLiquidity> VenueLiquidity { get; init; }

        /// <summary>Composite best bid price (across all venues)</summary>
        public long CompositeBestBidPrice { get; init; }

        /// <summary>Composite best bid size</summary>
        public int CompositeBestBidSize { get; init; }

        /// <summary>Composite best ask price</summary>
        public long CompositeBestAskPrice { get; init; }

        /// <summary>Composite best ask size</summary>
        public int CompositeBestAskSize { get; init; }

        /// <summary>Composite mid price</summary>
        public double CompositeMidPrice { get; init; }

        /// <summary>Composite spread (bps)</summary>
        public double CompositeSpreadBps { get; init; }
    }

    /// <summary>
    /// Liquidity at a specific venue.
    /// </summary>
    public readonly struct VenueLiquidity
    {
        /// <summary>Venue ID</summary>
        public string VenueId { get; init; }

        /// <summary>Best bid price at venue</summary>
        public long BestBidPrice { get; init; }

        /// <summary>Best bid size at venue</summary>
        public int BestBidSize { get; init; }

        /// <summary>Best ask price at venue</summary>
        public long BestAskPrice { get; init; }

        /// <summary>Best ask size at venue</summary>
        public int BestAskSize { get; init; }

        /// <summary>Visible depth at best bid (sum of top levels)</summary>
        public int BidDepth { get; init; }

        /// <summary>Visible depth at best ask (sum of top levels)</summary>
        public int AskDepth { get; init; }

        /// <summary>Whether venue has quotes</summary>
        public bool HasQuotes => BestBidPrice > 0 && BestAskPrice > 0;

        /// <summary>Mid price at venue</summary>
        public double MidPrice => HasQuotes ? (BestBidPrice + BestAskPrice) / 2.0 : 0;

        /// <summary>Spread at venue (bps)</summary>
        public double SpreadBps => HasQuotes ? ((BestAskPrice - BestBidPrice) / MidPrice) * 10000 : 0;
    }

    /// <summary>
    /// Router statistics for monitoring and optimization.
    /// </summary>
    public readonly struct RouterStatistics
    {
        /// <summary>Total orders routed</summary>
        public long TotalOrdersRouted { get; init; }

        /// <summary>Total quantity routed</summary>
        public long TotalQuantityRouted { get; init; }

        /// <summary>Total quantity filled</summary>
        public long TotalQuantityFilled { get; init; }

        /// <summary>Overall fill rate</summary>
        public double OverallFillRate => TotalQuantityRouted > 0 
            ? (double)TotalQuantityFilled / TotalQuantityRouted : 0;

        /// <summary>Average implementation shortfall (bps)</summary>
        public double AvgImplementationShortfallBps { get; init; }

        /// <summary>Average effective spread (bps)</summary>
        public double AvgEffectiveSpreadBps { get; init; }

        /// <summary>Average routing decision latency (microseconds)</summary>
        public double AvgDecisionLatencyUs { get; init; }

        /// <summary>P50 decision latency</summary>
        public double P50DecisionLatencyUs { get; init; }

        /// <summary>P99 decision latency</summary>
        public double P99DecisionLatencyUs { get; init; }

        /// <summary>Number of active parent orders</summary>
        public int ActiveParentOrders { get; init; }

        /// <summary>Number of registered venues</summary>
        public int RegisteredVenueCount { get; init; }

        /// <summary>Number of venues currently available</summary>
        public int AvailableVenueCount { get; init; }

        /// <summary>Total routing decisions logged</summary>
        public long TotalRoutingDecisions { get; init; }
    }

    /// <summary>
    /// Routing strategy parameters that can be updated at runtime.
    /// </summary>
    public readonly struct RoutingStrategyParameters
    {
        /// <summary>Weight for fill probability in utility calculation</summary>
        public double FillProbabilityWeight { get; init; }

        /// <summary>Weight for price improvement in utility calculation</summary>
        public double PriceImprovementWeight { get; init; }

        /// <summary>Weight for fees in utility calculation</summary>
        public double FeesWeight { get; init; }

        /// <summary>Weight for latency in utility calculation</summary>
        public double LatencyWeight { get; init; }

        /// <summary>Minimum expected utility threshold</summary>
        public double MinExpectedUtility { get; init; }

        /// <summary>Maximum venues to route to per slice</summary>
        public int MaxVenuesPerSlice { get; init; }

        /// <summary>Minimum allocation per venue</summary>
        public int MinAllocationPerVenue { get; init; }

        /// <summary>Rebalance threshold (when to redistribute)</summary>
        public double RebalanceThreshold { get; init; }

        /// <summary>
        /// Creates default strategy parameters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RoutingStrategyParameters Default()
        {
            return new RoutingStrategyParameters
            {
                FillProbabilityWeight = 1.0,
                PriceImprovementWeight = 0.5,
                FeesWeight = 0.3,
                LatencyWeight = 0.1,
                MinExpectedUtility = 0.01,
                MaxVenuesPerSlice = 5,
                MinAllocationPerVenue = 100,
                RebalanceThreshold = 0.10 // 10% drift triggers rebalance
            };
        }
    }
}

