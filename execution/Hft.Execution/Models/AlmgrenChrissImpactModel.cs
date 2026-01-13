using System;
using Hft.Core;

namespace Hft.Execution.Models;

/// <summary>
/// Almgren-Chriss market impact model.
/// Analytic model for optimal execution with permanent and temporary impact.
/// GRANDMASTER: Based on "Optimal Execution of Portfolio Transactions" (2000).
/// </summary>
public sealed class AlmgrenChrissImpactModel : IImpactModel
{
    private readonly double _gamma; // Permanent impact coefficient
    private readonly double _eta;   // Temporary impact coefficient

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="gamma">Permanent impact coefficient</param>
    /// <param name="eta">Temporary impact coefficient</param>
    public AlmgrenChrissImpactModel(double gamma = 0.0001, double eta = 0.001)
    {
        _gamma = gamma;
        _eta = eta;
    }

    /// <inheritdoc/>
    public ImpactResult ComputeImpact(
        OrderSide side,
        double quantity,
        MarketLiquidity liquidity,
        TimeScale scale)
    {
        ArgumentNullException.ThrowIfNull(liquidity);

        // Permanent impact: gamma * quantity
        double permanentImpact = _gamma * quantity;

        // Temporary impact: eta * (quantity / tau)
        // tau is the time interval (adjusted by scale)
        double tau = scale switch
        {
            TimeScale.Milliseconds => 0.001,
            TimeScale.Seconds => 1.0,
            TimeScale.Minutes => 60.0,
            _ => 1.0
        };

        double temporaryImpact = _eta * (quantity / tau);

        // Adjust for liquidity imbalance
        double imbalanceFactor = 1.0 + Math.Abs(liquidity.Imbalance) * 0.5;
        temporaryImpact *= imbalanceFactor;

        double totalCost = (permanentImpact + temporaryImpact) * quantity;

        return new ImpactResult(permanentImpact, temporaryImpact, totalCost);
    }
}
