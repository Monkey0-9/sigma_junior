using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Hft.Core;
using Hft.OrderBook;

namespace Hft.Routing
{
    /// <summary>
    /// Simulator for validating execution strategies against historical/mock market data.
    /// Provides Transaction Cost Analysis (TCA) and performance attribution.
    /// </summary>
    public sealed class ExecutionSimulator
    {
        private readonly ISmartOrderRouter _router;
        private readonly List<MarketDataSnapshot> _marketHistory;
        private readonly List<ExecutionResult> _results = new();

        public ExecutionSimulator(ISmartOrderRouter router, List<MarketDataSnapshot> marketHistory)
        {
            _router = router;
            _marketHistory = marketHistory;
        }

        public SimulationReport Run(ParentOrder parentOrder, long totalDurationUs)
        {
            int remainingQuantity = parentOrder.TotalQuantity;
            long startTimestamp = _marketHistory[0].Timestamp;
            long endTimestamp = startTimestamp + totalDurationUs;
            
            var strategy = SlicingStrategyFactory.Create(parentOrder, totalDurationUs);
            
            int totalFilled = 0;
            double totalShortfall = 0;
            double totalFees = 0;
            long currentTimestamp = startTimestamp;

            foreach (var marketData in _marketHistory)
            {
                if (marketData.Timestamp > endTimestamp || remainingQuantity <= 0)
                    break;

                long elapsedTime = marketData.Timestamp - startTimestamp;
                currentTimestamp = marketData.Timestamp;

                // 1. Compute next slice
                var slice = strategy.ComputeSlice(
                    parentOrder, remainingQuantity, marketData, elapsedTime, totalDurationUs);

                if (slice.Quantity <= 0)
                    continue;

                // 2. Wrap slice into a partial parent order for the router
                var partialParent = ParentOrder.Create(
                    parentOrder.ParentOrderId,
                    parentOrder.InstrumentId,
                    parentOrder.Side,
                    slice.Quantity,
                    slice.LimitPrice,
                    parentOrder.Intent,
                    slice.TimeInForce,
                    parentOrder.Strategy,
                    parentOrder.StrategyParameters);

                // 3. Compute routing plan
                var liquidity = CreateLiquiditySnapshot(marketData);
                var plan = _router.ComputeRoutingPlan(partialParent, liquidity);

                // 4. Execute plan
                var result = _router.ExecutePlan(plan);
                _results.Add(result);

                // 5. Update state
                totalFilled += result.FilledQuantity;
                remainingQuantity -= result.FilledQuantity;
                totalShortfall += result.ImplementationShortfallBps * result.FilledQuantity;
                totalFees += result.TotalFeesBps * result.FilledQuantity;

                // 6. Provide feedback to strategy
                strategy.UpdateFeedback(SliceFeedback.Create(
                    slice.Quantity, result.FilledQuantity, result.AverageFillPrice, 
                    result.ExecutionDurationMicroseconds, result.ImplementationShortfallBps));
            }

            double avgShortfall = totalFilled > 0 ? totalShortfall / totalFilled : 0;
            double avgFees = totalFilled > 0 ? totalFees / totalFilled : 0;
            double fillRate = (double)totalFilled / parentOrder.TotalQuantity;

            return new SimulationReport
            {
                TotalQuantity = parentOrder.TotalQuantity,
                FilledQuantity = totalFilled,
                FillRate = fillRate,
                AvgImplementationShortfallBps = avgShortfall,
                AvgFeesBps = avgFees,
                DurationMicroseconds = currentTimestamp - startTimestamp,
                ResultCount = _results.Count
            };
        }

        private LiquiditySnapshot CreateLiquiditySnapshot(MarketDataSnapshot md)
        {
            var venueLiq = new List<VenueLiquidity>();
            foreach (var vbbo in md.VenueBbos)
            {
                venueLiq.Add(new VenueLiquidity
                {
                    VenueId = "EXCH_" + vbbo.InstrumentId, // Mock ID
                    BestBidPrice = vbbo.BestBidPrice,
                    BestBidSize = vbbo.BestBidSize,
                    BestAskPrice = vbbo.BestAskPrice,
                    BestAskSize = vbbo.BestAskSize,
                    BidDepth = vbbo.BestBidSize * 5, // Mock depth
                    AskDepth = vbbo.BestAskSize * 5
                });
            }

            return new LiquiditySnapshot
            {
                InstrumentId = md.InstrumentId,
                Timestamp = md.Timestamp,
                VenueLiquidity = venueLiq,
                CompositeBestBidPrice = md.BestBidPrice,
                CompositeBestAskPrice = md.BestAskPrice,
                CompositeMidPrice = md.MidPrice,
                CompositeSpreadBps = md.SpreadBps
            };
        }
    }

    public struct SimulationReport
    {
        public int TotalQuantity { get; init; }
        public int FilledQuantity { get; init; }
        public double FillRate { get; init; }
        public double AvgImplementationShortfallBps { get; init; }
        public double AvgFeesBps { get; init; }
        public long DurationMicroseconds { get; init; }
        public int ResultCount { get; init; }

        public override string ToString() => 
            $"Simulation Result: FillRate={FillRate:P2}, Shortfall={AvgImplementationShortfallBps:F2} bps, Fees={AvgFeesBps:F2} bps";
    }
}
