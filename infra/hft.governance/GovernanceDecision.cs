using System;

namespace Hft.Governance
{
    /// <summary>
    /// Immutable record of a governance decision.
    /// Used for audit trails and policy enforcement.
    /// </summary>
    public sealed record GovernanceDecision
    {
        public Guid DecisionId { get; init; } = Guid.NewGuid();
        public long Timestamp { get; init; } = DateTime.UtcNow.Ticks;
        public string PolicyName { get; init; } = string.Empty;
        public string ApproverId { get; init; } = string.Empty;
        public bool IsApproved { get; init; }
        public string Rationale { get; init; } = string.Empty;
        public string Signature { get; init; } = string.Empty; // HMAC or Digital Signature
    }
}
