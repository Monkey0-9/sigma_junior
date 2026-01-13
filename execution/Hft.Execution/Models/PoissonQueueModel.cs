using System;
using Hft.Core;

namespace Hft.Execution.Models;

/// <summary>
/// Queue model based on Poisson arrival processes.
/// Simulates queue depletion via trades and cancels.
/// GRANDMASTER: Stochastic queue model for realistic execution logic.
/// </summary>
public sealed class PoissonQueueModel : IQueueModel
{
    private readonly IRandomProvider _random;
    private readonly double _cancelRate; // Percentage of volume removed by cancels

    public PoissonQueueModel(IRandomProvider random, double cancelRate = 0.1)
    {
        _random = random;
        _cancelRate = cancelRate;
    }

    /// <inheritdoc/>
    public int EstimateInitialQueuePosition(OrderSide side, long price, MarketLiquidity liquidity)
    {
        // Simple estimation: assume we are at the back of the queue at this price level
        // In a real model, this would look at the specific price level size
        // Here we use a simplified heuristic based on total depth
        
        // Use deterministic random to simulate some variance in where we land 
        // (e.g. latency arbitrage might put others ahead of us even if we think we are first)
        
        double jitter = _random.NextDouble() * 0.2; // 0-20% uncertainty
        int basePosition = 1000; // Placeholder for actual level size logic
        
        return (int)(basePosition * (1.0 + jitter));
    }

    /// <inheritdoc/>
    public int UpdateQueuePosition(int currentPosition, double tradeVolume, double cancellationVolume)
    {
        if (currentPosition <= 0) return 0;
        
        // Decrease position by trades and a portion of cancels
        // Assumes cancels happen uniformly throughout the queue
        
        double depletion = tradeVolume + (cancellationVolume * _cancelRate);
        int newPosition = (int)Math.Max(0, currentPosition - depletion);
        
        return newPosition;
    }

    /// <inheritdoc/>
    public double CalculateFillProbability(int queuePosition, double quantity)
    {
        if (queuePosition <= 0) return 1.0;
        
        // Exponential decay of fill probability as a function of queue position
        // This is a simplified proxy for "time to fill"
        return Math.Exp(-0.0001 * queuePosition);
    }
}
