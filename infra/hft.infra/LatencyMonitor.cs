using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Hft.Infra
{
    /// <summary>
    /// Institutional Latency Monitor.
    /// Tracks Tick-to-Order latency with nanosecond precision.
    /// Computes summary statistics for p50, p90, p99.
    /// GRANDMASTER: Sealed class for performance and security (CA1052).
    /// </summary>
    public sealed class LatencyMonitor
    {
        private readonly List<long> _samples = new(10000);
        private readonly object _lock = new();

        public void Record(long ticks)
        {
            lock (_lock)
            {
                _samples.Add(ticks);
            }
        }

        public LatencyStats GetStats()
        {
            lock (_lock)
            {
                if (_samples.Count == 0) return new LatencyStats();

                var sorted = _samples.OrderBy(s => s).ToList();
                return new LatencyStats
                {
                    Count = sorted.Count,
                    Min = sorted[0] / 10.0, // Convert Ticks (100ns) to Microseconds
                    Max = sorted[sorted.Count - 1] / 10.0,
                    Avg = sorted.Average() / 10.0,
                    P50 = sorted[(int)(sorted.Count * 0.50)] / 10.0,
                    P90 = sorted[(int)(sorted.Count * 0.90)] / 10.0,
                    P99 = sorted[(int)(sorted.Count * 0.99)] / 10.0
                };
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _samples.Clear();
            }
        }
    }

    public struct LatencyStats : IEquatable<LatencyStats>
    {
        public int Count { get; init; }
        public double Min { get; init; }
        public double Max { get; init; }
        public double Avg { get; init; }
        public double P50 { get; init; }
        public double P90 { get; init; }
        public double P99 { get; init; }

        public readonly bool Equals(LatencyStats other) =>
            Count == other.Count && Min == other.Min && Max == other.Max &&
            Avg == other.Avg && P50 == other.P50 && P90 == other.P90 && P99 == other.P99;

        public readonly override bool Equals(object? obj) =>
            obj is LatencyStats other && Equals(other);

        public readonly override int GetHashCode() =>
            HashCode.Combine(Count, Min, Max, Avg, P50, P90, P99);

        public static bool operator ==(LatencyStats left, LatencyStats right) =>
            left.Equals(right);

        public static bool operator !=(LatencyStats left, LatencyStats right) =>
            !left.Equals(right);

        public override string ToString()
        {
            return $"Count={Count}, Min={Min:F2}us, Avg={Avg:F2}us, P99={P99:F2}us";
        }
    }
}

