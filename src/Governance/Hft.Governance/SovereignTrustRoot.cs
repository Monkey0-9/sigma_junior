using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Hft.Governance
{
    /// <summary>
    /// Layer 0: Sovereign Trust Root.
    /// Manages cryptographic identity, policy-carrying decisions, and formal constraints.
    /// </summary>
    public class SovereignTrustRoot
    {
        private readonly ConcurrentDictionary<string, IPolicy> _policies = new();
        private readonly List<string> _auditTrail = new();

        public void RegisterPolicy(string policyId, IPolicy policy)
        {
            _policies[policyId] = policy;
        }

        public Proof VerifyDecision(DecisionRequest request)
        {
            foreach (var policy in _policies.Values)
            {
                if (!policy.Allows(request))
                {
                    return Proof.Reject(policy.Name, policy.ConstraintDescription);
                }
            }
            return Proof.Approve();
        }
    }

    public interface IPolicy
    {
        string Name { get; }
        string ConstraintDescription { get; }
        bool Allows(DecisionRequest request);
    }

    public struct DecisionRequest
    {
        public string Action;
        public object Context;
        public long Timestamp;
    }

    public record Proof(bool IsApproved, string? Reason = null, string? Constraint = null)
    {
        public static Proof Approve() => new(true);
        public static Proof Reject(string reason, string constraint) => new(false, reason, constraint);
    }
}
