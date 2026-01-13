using System;
using System.Collections.Generic;
using Hft.Core;

namespace Hft.Execution.Models;

/// <summary>
/// Deterministic matching engine using price-time priority.
/// Orchestrates Queue, Impact, and Latency models to produce execution reports.
/// </summary>
public sealed class DeterministicMatchingEngine : IMatchingEngine
{
    private readonly IQueueModel _queueModel;
    private readonly IImpactModel _impactModel;
    private readonly ILatencyModel _latencyModel;
    private readonly ITimeProvider _timeProvider;

    public DeterministicMatchingEngine(
        IQueueModel queueModel,
        IImpactModel impactModel,
        ILatencyModel latencyModel,
        ITimeProvider timeProvider)
    {
        _queueModel = queueModel;
        _impactModel = impactModel;
        _latencyModel = latencyModel;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public IEnumerable<ExecutionReport> ProcessOrder(OrderQueueEntry order, MarketLiquidity liquidity)
    {
        // 1. Simulate Latency (Order arriving at exchange)
        double latencyInbound = _latencyModel.GenerateLatency();
        long exchangeArrivalTime = _timeProvider.EpochMicroseconds + (long)latencyInbound; // All us

        // 2. Queue Position
        int queuePos = _queueModel.EstimateInitialQueuePosition(order.Side, order.Price, liquidity);

        // 3. Impact Assessment (Immediate for marketable orders)
        // For Limit orders, impact might affect whether we get filled or not by moving the price away
        if (order.Type == OrderType.Market || IsMarketable(order, liquidity))
        {
             // Immediate execution logic
             ImpactResult impact = _impactModel.ComputeImpact(order.Side, order.LeavesQuantity, liquidity, TimeScale.Milliseconds);
             
             // In a full simulation, we'd walk the book. Here we use impact result to adjust price
             double fillPrice = impact.TotalCost / order.LeavesQuantity; // Avg price
             
             yield return new ExecutionReport(
                 IsFilled: true,
                 FilledQuantity: order.LeavesQuantity,
                 LimitPrice: order.Price,
                 Price: fillPrice,
                 Fee: ComputeFee(fillPrice, order.LeavesQuantity, true),
                 Timestamp: exchangeArrivalTime
             );
        }
        else
        {
            // Passive Limit Order logic
            // In a real simulation, we would yield return nothing initially, 
            // and future updates would drive fills based on queue depletion.
            // For this interface, we might just return the 'ack' or initial state?
            // Or maybe this method assumes a "snapshot" execution which resolves immediately?
            // "ProcessOrder" implies immediate processing.
            
            // For passive orders, we just acknowledge receipt in this simple step.
            // Real passive fill simulation requires a time-stepping loop which is outside this atomic method.
             yield return new ExecutionReport(
                 IsFilled: false,
                 FilledQuantity: 0,
                 LimitPrice: order.Price,
                 Price: 0,
                 Fee: 0,
                 Timestamp: exchangeArrivalTime
             );
        }
    }

    private static bool IsMarketable(OrderQueueEntry order, MarketLiquidity liquidity)
    {
        // Simple check against best opposite price
        // (Assuming liquidity arrays are [best, next, ...])
        if (order.Side == OrderSide.Buy)
        {
            return liquidity.AskDepth.Length > 0 && order.Price >= 100; // Placeholder price check
        }
        else
        {
            return liquidity.BidDepth.Length > 0 && order.Price <= 100; // Placeholder
        }
    }

    private static double ComputeFee(double price, double quantity, bool isTaker)
    {
        // Simplified fee model
        double rate = isTaker ? 0.0003 : -0.0001; 
        return price * quantity * rate;
    }
}
