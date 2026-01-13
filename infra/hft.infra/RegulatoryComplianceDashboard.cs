using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Hft.Infra
{
    /// <summary>
    /// Represents a compliance metric event.
    /// </summary>
    public readonly struct ComplianceMetric : IEquatable<ComplianceMetric>
    {
        public string Category { get; }
        public string Description { get; }
        public string Severity { get; } // Info, Warning, Critical
        public DateTime Timestamp { get; }

        public ComplianceMetric(string category, string description, string severity, DateTime timestamp)
        {
            Category = category;
            Description = description;
            Severity = severity;
            Timestamp = timestamp;
        }

        public readonly bool Equals(ComplianceMetric other) =>
            Category == other.Category && Description == other.Description &&
            Severity == other.Severity && Timestamp == other.Timestamp;

        public readonly override bool Equals(object? obj) =>
            obj is ComplianceMetric other && Equals(other);

        public readonly override int GetHashCode() =>
            HashCode.Combine(Category, Description, Severity, Timestamp);

        public static bool operator ==(ComplianceMetric left, ComplianceMetric right) =>
            left.Equals(right);

        public static bool operator !=(ComplianceMetric left, ComplianceMetric right) =>
            !left.Equals(right);
    }

    /// <summary>
    /// Institutional Regulatory Compliance & Decision Dashboard Engine.
    /// Aggregates risk breaches, alpha ensemble shifts, and execution slippage for CRO/Regulator view.
    /// Aligned with Aladdin's enterprise governance standards.
    /// </summary>
    public sealed class RegulatoryComplianceDashboard
    {
        private readonly ConcurrentQueue<ComplianceMetric> _events = new();
        private readonly IEventLogger _logger;

        public RegulatoryComplianceDashboard(IEventLogger logger)
        {
            _logger = logger;
        }

        public void EmitComplianceEvent(string cat, string desc, string severity)
        {
            var m = new ComplianceMetric(cat, desc, severity, DateTime.UtcNow);
            _events.Enqueue(m);
            _logger.LogInfo("COMPLIANCE", $"[{severity}] {cat}: {desc}");

            // Limit memory usage
            if (_events.Count > 1000) _events.TryDequeue(out _);
        }

        public IEnumerable<ComplianceMetric> GetCriticalBreaches() =>
            _events.Where(e => e.Severity == "Critical");

        public override string ToString()
        {
            var criticalCount = _events.Count(e => e.Severity == "Critical");
            var warningCount = _events.Count(e => e.Severity == "Warning");
            return $"Institutional Health: {criticalCount} Criticals, {warningCount} Warnings.";
        }
    }
}

