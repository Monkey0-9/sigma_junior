using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Hft.Governance.Services
{
    public class PolicyState
    {
        private readonly ConcurrentDictionary<string, GovernanceDecision> _policies = new();

        public void AddDecision(GovernanceDecision decision)
        {
            if (decision.IsApproved)
            {
                _policies[decision.PolicyName] = decision;
            }
            else
            {
                _policies.TryRemove(decision.PolicyName, out _);
            }
        }

        public GovernanceDecision? GetPolicy(string name)
        {
            _policies.TryGetValue(name, out var decision);
            return decision;
        }
    }
}
