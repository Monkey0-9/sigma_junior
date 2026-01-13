using System;
using Hft.Core;

namespace Hft.Execution.Models;

/// <summary>
/// Interface for queue position modeling.
/// Estimates probability of fill based on queue dynamics.
/// GRANDMASTER: Pluggable queue dynamics for realistic order placement.
/// </summary>
public interface IQueueModel
{
    /// <summary>
    /// Estimates initial queue position for a new order.
    /// </summary>
    int EstimateInitialQueuePosition(OrderSide side, long price, MarketLiquidity liquidity);

    /// <summary>
    /// Updates queue position based on trades and cancellations.
    /// </summary>
    /// <returns>New estimated queue position</returns>
    int UpdateQueuePosition(int currentPosition, double tradeVolume, double cancellationVolume);
    
    /// <summary>
    /// Calculates probability of fill given current queue position.
    /// </summary>
    double CalculateFillProbability(int queuePosition, double quantity);
}
