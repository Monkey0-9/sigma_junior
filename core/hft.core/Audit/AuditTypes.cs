using System;

namespace Hft.Core.Audit
{
    /// <summary>
    /// Institutional Audit Record Types.
    /// Standardized event identifiers for forensic analysis.
    /// </summary>
    public enum AuditRecordType : int
    {
        None = 0,
        OrderSubmit = 1,
        OrderReject = 2,
        OrderCancel = 3,
        Fill = 4,
        RiskViolation = 5,
        PnlUpdate = 6,
        Tick = 7,
        SystemEvent = 255
    }

    /// <summary>
    /// Institutional Audit Record.
    /// Represents a single immutable event in the audit trail.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "DTO")]
    public readonly record struct AuditRecord(long Timestamp, byte Type, byte[] Payload);
}
