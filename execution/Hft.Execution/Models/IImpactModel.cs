using System.Collections.Generic;

namespace Hft.Execution.Models;

/// <summary>
/// Market liquidity snapshot for impact calculation.
/// GRANDMASTER: Immutable record for deterministic impact modeling.
/// </summary>
public record MarketLiquidity(
    IReadOnlyList<double> BidDepth,
    IReadOnlyList<double> AskDepth,
    double VWAPWindow,
    double Imbalance
);

/// <summary>
/// Result of market impact calculation.
/// </summary>
public record ImpactResult(
    double PermanentImpact,
    double TemporaryImpact,
    double TotalCost
);

/// <summary>
/// Time scale for impact calculation.
/// </summary>
public enum TimeScale
{
    Milliseconds,
    Seconds,
    Minutes
}

/// <summary>
/// Interface for market impact models.
/// GRANDMASTER: Pluggable impact calculation for ERE v2.
/// </summary>
public interface IImpactModel
{
    /// <summary>
    /// Computes market impact for a given order.
    /// </summary>
    ImpactResult ComputeImpact(
        Hft.Core.OrderSide side,
        double quantity,
        MarketLiquidity liquidity,
        TimeScale scale);
}
