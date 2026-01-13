using System.Collections.Generic;
using Hft.Core;

namespace Hft.Risk.Kernel;

/// <summary>
/// Result of a risk check.
/// GRANDMASTER: Detailed result including reason and potential override.
/// </summary>
public record RiskCheckResult(
    bool Allowed,
    string Reason,
    string CheckId,
    double ConfidenceScore,
    bool RequiresGovernanceApproval
);

/// <summary>
/// Interface for the Risk-as-OS Kernel.
/// The central authority for all trading decisions.
/// </summary>
public interface IRiskKernel
{
    /// <summary>
    /// Validates an order against all active risk models.
    /// </summary>
    RiskCheckResult ValidateOrder(ParentOrder order, PortfolioState portfolio);
    
    /// <summary>
    /// Registers a new risk model with the kernel.
    /// </summary>
    void RegisterModel(IRiskModel model);
    
    /// <summary>
    /// Gets the current kernel governance state.
    /// </summary>
    GovernanceState GetState();
}

/// <summary>
/// Interface for individual risk models.
/// </summary>
public interface IRiskModel
{
    string ModelId { get; }
    RiskCheckResult Check(ParentOrder order, PortfolioState portfolio);
}

public enum GovernanceState
{
    Normal,
    HeightenedScrutiny,
    KillSwitchActive,
    Maintenance
}

// Placeholder for PortfolioState if not already defined in Core
public class PortfolioState
{
    // Implementation details...
}
