using System;

namespace Hft.Governance
{
    /// <summary>
    /// Formal Stability Policy based on Lyapunov Control Theory.
    /// Invariant: Risk(x_t) + lambda * D^2 must non-increase.
    /// </summary>
    public class StabilityPolicy : IPolicy
    {
        public string Name => "LyapunovStabilityGate";
        public string ConstraintDescription => "dV/dt <= 0 (Symmetric positive definite stability)";

        private double _lastV = double.MaxValue;

        public bool Allows(DecisionRequest request)
        {
            if (request.Context is SystemState state)
            {
                double v = ComputeLyapunov(state);
                if (v > _lastV) return false; // Potential instability
                
                _lastV = v;
                return true;
            }
            return false;
        }

        private double ComputeLyapunov(SystemState state)
        {
            // V(x) = x^T Sigma x + lambda * Drawdown^2
            // For now, a simplified model for demonstration
            return (state.Leverage * state.Leverage * state.Volatility) + (0.5 * state.MaxDrawdown * state.MaxDrawdown);
        }
    }

    public struct SystemState
    {
        public double Leverage;
        public double Volatility;
        public double MaxDrawdown;
    }
}
