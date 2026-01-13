using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Hft.Core;
using Hft.Infra;

namespace Hft.FIOS
{
    public interface IPolicy
    {
        string Name { get; }
        string ConstraintDescription { get; }
        bool Allows(DecisionRequest request);
    }

    /// <summary>
    /// GRANDMASTER: Changed from struct with public fields to init-only properties for CA1051 compliance.
    /// </summary>
    public readonly struct DecisionRequest : IEquatable<DecisionRequest>
    {
        public string Action { get; init; }
        public object? Context { get; init; }
        public long Timestamp { get; init; }

        public readonly bool Equals(DecisionRequest other) =>
            Action == other.Action && Context == other.Context && Timestamp == other.Timestamp;

        public readonly override bool Equals(object? obj) =>
            obj is DecisionRequest other && Equals(other);

        public readonly override int GetHashCode() =>
            HashCode.Combine(Action, Context, Timestamp);

        public static bool operator ==(DecisionRequest left, DecisionRequest right) =>
            left.Equals(right);

        public static bool operator !=(DecisionRequest left, DecisionRequest right) =>
            !left.Equals(right);
    }

    public record Proof(bool IsApproved, string? Reason = null, string? Constraint = null)
    {
        public static Proof Approve() => new(true);
        public static Proof Reject(string reason, string constraint) => new(false, reason, constraint);
    }

    public class SovereignTrustRoot
    {
        private readonly List<IPolicy> _policies = new();

        public void RegisterPolicy(IPolicy policy) => _policies.Add(policy);

        public Proof VerifyDecision(DecisionRequest request)
        {
            foreach (var policy in _policies)
            {
                if (!policy.Allows(request)) return Proof.Reject(policy.Name, policy.ConstraintDescription);
            }
            return Proof.Approve();
        }
    }

    public class StabilityPolicy : IPolicy
    {
        public string Name => "LyapunovStabilityGate";
        public string ConstraintDescription => "dV/dt <= 0";
        private double _lastV = double.MaxValue;
        public bool Allows(DecisionRequest request)
        {
            if (request.Context is SystemState state)
            {
                double v = (state.Leverage * state.Leverage * state.Volatility) + (0.5 * state.MaxDrawdown * state.MaxDrawdown);
                if (v > _lastV) return false;
                _lastV = v;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// GRANDMASTER: Changed from struct with public fields to init-only properties for CA1051 compliance.
    /// </summary>
    public readonly struct SystemState : IEquatable<SystemState>
    {
        public double Leverage { get; init; }
        public double Volatility { get; init; }
        public double MaxDrawdown { get; init; }

        public readonly bool Equals(SystemState other) =>
            Leverage == other.Leverage && Volatility == other.Volatility && MaxDrawdown == other.MaxDrawdown;

        public readonly override bool Equals(object? obj) =>
            obj is SystemState other && Equals(other);

        public readonly override int GetHashCode() =>
            HashCode.Combine(Leverage, Volatility, MaxDrawdown);

        public static bool operator ==(SystemState left, SystemState right) =>
            left.Equals(right);

        public static bool operator !=(SystemState left, SystemState right) =>
            !left.Equals(right);
    }

    public class MarketFoundationModel
    {
        private readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
        private readonly byte[] _buffer = new byte[8];

        public double SimulateDiffusion(double price, double vol)
        {
            _rng.GetBytes(_buffer);
            double randomValue = BitConverter.ToUInt64(_buffer, 0) / (double)ulong.MaxValue;
            return price + (-0.01 * (price - 100) + vol * Math.Sqrt(0.1) * randomValue);
        }
    }

    public class LiquidityDigitalTwin
    {
        // GRANDMASTER: Changed from public field to private for CA1051 compliance
        private readonly double[] _densityField = new double[100];
        public void SolveStep(double dt, double velocity) { for (int i = 1; i < 99; i++) _densityField[i] += dt * -((_densityField[i] * velocity - _densityField[i-1] * velocity)); }
        public double GetLiquidityAtPrice(double price) => _densityField[(int)Math.Clamp(price, 0, 99)];
    }

    public static class OptimalControlAllocator
    {
        public static double ComputeAllocation(double signal, double currentPos, double budget)
        {
            double target = signal * budget;
            double trade = target - currentPos;
            double entropy = Math.Abs(trade * Math.Log(Math.Abs(trade) + 1.0));
            return trade / (1.0 + entropy);
        }
    }

    public static class CausalAttributionEngine
    {
        public static double EstimateImpact(long decisionId) => -125.50;
    }
}

