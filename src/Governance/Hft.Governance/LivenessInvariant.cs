using System;

namespace Hft.Governance
{
    /// <summary>
    /// Formal Liveness Invariant.
    /// Ensures that the system does not enter a "frozen" state.
    /// Constraint: Diamond(Rebalance) - Rebalance must eventually happen.
    /// </summary>
    public class LivenessInvariant : IPolicy
    {
        public string Name => "LivenessInvariant";
        public string ConstraintDescription => "Diamond(Rebalance) - Progress must be observable";

        private long _lastRebalanceTicks = DateTime.UtcNow.Ticks;
        private const long MaxStaleperiodTicks = 10 * TimeSpan.TicksPerMinute;

        public bool Allows(DecisionRequest request)
        {
            if (request.Action == "Rebalance")
            {
                _lastRebalanceTicks = DateTime.UtcNow.Ticks;
                return true;
            }

            // Check if system has been stale for too long
            if (DateTime.UtcNow.Ticks - _lastRebalanceTicks > MaxStaleperiodTicks)
            {
                return false; // Forbidden State: System is stale
            }

            return true;
        }
    }
}
