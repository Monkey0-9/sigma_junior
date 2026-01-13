using System;

namespace Hft.Execution.Models;

/// <summary>
/// Interface for adverse selection modeling.
/// Simulates "toxic" flow where price moves against you immediately after fill.
/// GRANDMASTER: Crucial for simulating HFT predator strategies.
/// </summary>
public interface IAdverseSelectionModel
{
    /// <summary>
    /// Calculates the adverse price movement following a trade.
    /// </summary>
    double CalculatePostTradeMoves(double tradeSize, double volatility);
}
