using System;

namespace Hft.Risk
{
    public enum RiskDecision
    {
        Allow,
        Throttle,
        Block
    }

    public record RiskEvidence
    {
        public string Rule { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
        public double CurrentValue { get; init; }
        public double Limit { get; init; }
    }

    public readonly record struct RiskCheckResult
    {
        public RiskDecision Decision { get; }
        public RiskEvidence? Evidence { get; }

        public RiskCheckResult(RiskDecision decision, RiskEvidence? evidence = null)
        {
            Decision = decision;
            Evidence = evidence;
        }

        public static RiskCheckResult Allowed() => new RiskCheckResult(RiskDecision.Allow);
        public static RiskCheckResult Blocked(string rule, string reason, double current, double limit) 
            => new RiskCheckResult(RiskDecision.Block, new RiskEvidence { Rule = rule, Reason = reason, CurrentValue = current, Limit = limit });
        public static RiskCheckResult Throttled(string rule, string reason)
            => new RiskCheckResult(RiskDecision.Throttle, new RiskEvidence { Rule = rule, Reason = reason });
    }
}
