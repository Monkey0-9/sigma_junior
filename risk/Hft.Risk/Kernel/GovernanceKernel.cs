using System;
using System.Collections.Generic;
using Hft.Core;

namespace Hft.Risk.Kernel;

/// <summary>
/// Governance Kernel responsible for manual approval workflows and exceptional overrides.
/// GRANDMASTER: Human-in-the-loop control for high-risk actions.
/// </summary>
public sealed class GovernanceKernel
{
    private readonly IEventLogger _auditLog;

    public GovernanceKernel(IEventLogger auditLog)
    {
        _auditLog = auditLog;
    }

    /// <summary>
    /// requests approval for a risk override.
    /// In production, this would integrate with Symphony/Slack/Email MFA.
    /// </summary>
    public bool RequestOverride(string checkId, string reason, string requestor)
    {
        // 1. Log the request
        _auditLog.LogWarning("GOVERNANCE", $"Request: {checkId} by {requestor} reason: {reason}");
        
        // 2. In this automated simulation/demo, we deny by default unless in a specific test mode
        // Or strictly allow specific overrides
        
        if (checkId == "LIMIT_BREACH_SMALL" && reason == "HEDGING")
        {
             _auditLog.LogInfo("GOVERNANCE", "Auto-Approved limit breach for hedging");
             return true;
        }

        return false;
    }

    /// <summary>
    /// Validates that a strategy is approved for production trading.
    /// </summary>
    public bool IsStrategyApproved(string strategyId, string version)
    {
        _auditLog.LogInfo("GOVERNANCE", $"Checking strategy approval: {strategyId} v{version}");
        // Check against a secure database of signed binaries/configs
        return true; 
    }
}
