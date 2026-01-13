using System;
using Hft.Core;

namespace Hft.Execution.Models;

/// <summary>
/// Square-root market impact model based on empirical research.
/// Formula: ImpactCost = k * sigma * (Q / V)^0.5
/// GRANDMASTER: Empirical model for realistic execution simulation.
/// </summary>
public sealed class SquareRootImpactModel : IImpactModel
{
    private readonly double _impactCoefficient;
    private readonly double _volatility;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="impactCoefficient">Impact coefficient k (typically 0.1-1.0)</param>
    /// <param name="volatility">Asset volatility sigma</param>
    public SquareRootImpactModel(double impactCoefficient = 0.5, double volatility = 0.02)
    {
        _impactCoefficient = impactCoefficient;
        _volatility = volatility;
    }

    /// <inheritdoc/>
    public ImpactResult ComputeImpact(
        OrderSide side,
        double quantity,
        MarketLiquidity liquidity,
        TimeScale scale)
    {
        ArgumentNullException.ThrowIfNull(liquidity);

        // Calculate total available volume
        double totalVolume = 0;
        foreach (var depth in side == OrderSide.Buy ? liquidity.AskDepth : liquidity.BidDepth)
        {
            totalVolume += depth;
        }

        if (totalVolume <= 0)
            totalVolume = quantity * 10; // Fallback

        // Square-root impact: cost = k * sigma * sqrt(Q / V)
        double participationRate = quantity / totalVolume;
        double sqrtImpact = _impactCoefficient * _volatility * Math.Sqrt(participationRate);

        // Permanent impact (50% of total)
        double permanentImpact = sqrtImpact * 0.5;

        // Temporary impact (50% of total, decays over time)
        double temporaryImpact = sqrtImpact * 0.5;

        // Adjust for time scale
        double timeScaleFactor = scale switch
        {
            TimeScale.Milliseconds => 1.5,
            TimeScale.Seconds => 1.0,
            TimeScale.Minutes => 0.7,
            _ => 1.0
        };

        temporaryImpact *= timeScaleFactor;

        double totalCost = (permanentImpact + temporaryImpact) * quantity;

        return new ImpactResult(permanentImpact, temporaryImpact, totalCost);
    }
}
