using System.Collections.Generic;

namespace Hft.Core
{
    /// <summary>
    /// Institutional abstraction for metrics collection.
    /// Supports both pull (Prometheus) and push (StatsD/OTLP) models.
    /// High-performance, allocation-free emission on hot paths.
    /// </summary>
    public interface IMetricsProvider
    {
        void RecordCounter(string name, long value);
        void RecordGauge(string name, double value);

        /// <summary>
        /// Returns all collected metrics (used by pull-based listeners).
        /// </summary>
        IEnumerable<MetricValue> GetMetrics();
    }

    public struct MetricValue : IEquatable<MetricValue>
    {
        public string Name { get; init; }
        public double Value { get; init; }
        public string Type { get; init; } // counter, gauge

        public readonly bool Equals(MetricValue other) =>
            Name == other.Name && Value == other.Value && Type == other.Type;

        public readonly override bool Equals(object? obj) =>
            obj is MetricValue other && Equals(other);

        public readonly override int GetHashCode() =>
            HashCode.Combine(Name, Value, Type);

        public static bool operator ==(MetricValue left, MetricValue right) =>
            left.Equals(right);

        public static bool operator !=(MetricValue left, MetricValue right) =>
            !left.Equals(right);
    }
}

