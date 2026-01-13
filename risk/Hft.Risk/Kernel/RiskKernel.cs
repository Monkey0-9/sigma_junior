using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Hft.Core;

namespace Hft.Risk.Kernel;

/// <summary>
/// The central nervous system of the Risk-as-OS architecture.
/// Coordinates all risk checks, stress tests, and governance rules.
/// GRANDMASTER: Authoritative source of truth for trading permissions.
/// </summary>
public sealed class RiskKernel : IRiskKernel
{
    private readonly ConcurrentDictionary<string, IRiskModel> _models;
    private readonly IEventLogger _auditLog;
    private volatile GovernanceState _state;
    private readonly object _stateLock = new object();

    public RiskKernel(IEventLogger auditLog)
    {
        _models = new ConcurrentDictionary<string, IRiskModel>();
        _auditLog = auditLog;
        _state = GovernanceState.Normal;
    }

    /// <inheritdoc/>
    public void RegisterModel(IRiskModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _models[model.ModelId] = model;
    }

    /// <inheritdoc/>
    public GovernanceState GetState() => _state;

    /// <inheritdoc/>
    public RiskCheckResult ValidateOrder(ParentOrder order, PortfolioState portfolio)
    {
        // 1. Check Kernel State (Kill Switch / Maintenance)
        if (_state == GovernanceState.KillSwitchActive)
        {
            return new RiskCheckResult(false, "Kill Switch Active", "KERNEL_KILL", 1.0, false);
        }
        
        if (_state == GovernanceState.Maintenance)
        {
             return new RiskCheckResult(false, "System Maintenance", "KERNEL_MAINT", 1.0, false);
        }

        // 2. Run all registered models in parallel or sequence
        // For HFT, sequence might be faster due to overhead, but let's assume we want fail-fast
        
        var results = new List<RiskCheckResult>();
        bool allowed = true;
        string firstFailureReason = string.Empty;

        foreach (var model in _models.Values)
        {
            var result = model.Check(order, portfolio);
            results.Add(result);
            
            if (!result.Allowed)
            {
                allowed = false;
                if (string.IsNullOrEmpty(firstFailureReason))
                {
                    firstFailureReason = result.Reason;
                }
                // Fail fast logic? Or collect all?
                // Let's fail fast for latency
                break; 
            }
        }

        // 3. Log decision (Audit Trail)
        // In a real system, this would be async or highly optimized
        // _auditLog.Log(...); 

        if (!allowed)
        {
            return new RiskCheckResult(false, firstFailureReason, "AGGREGATE_FAIL", 1.0, false);
        }

        return new RiskCheckResult(true, "Approved", "KERNEL_OK", 1.0, false);
    }
}
