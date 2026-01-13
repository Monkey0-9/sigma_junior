using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Hft.Core
{
    /// <summary>
    /// Centralized store for platform-wide metrics.
    /// Thread-safe and high-performance, designed for HFT monitoring.
    /// </summary>
    public sealed class CentralMetricsStore
    {
        private static readonly Lazy<CentralMetricsStore> _instance = 
            new(() => new CentralMetricsStore());

        public static CentralMetricsStore Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, MetricsCounter> _counters = new();
        private readonly ConcurrentDictionary<string, double> _gauges = new();
        
        // Histograms (simplified)
        private readonly ConcurrentDictionary<string, LatencyTracker> _latencyTrackers = new();

        private CentralMetricsStore() { }

        public MetricsCounter GetCounter(string name)
        {
            return _counters.GetOrAdd(name, (n) => new MetricsCounter(n));
        }

        public void SetGauge(string name, double value)
        {
            _gauges[name] = value;
        }

        public double GetGauge(string name)
        {
            return _gauges.TryGetValue(name, out var val) ? val : 0;
        }

        public void RecordLatency(string name, double microseconds)
        {
            var tracker = _latencyTrackers.GetOrAdd(name, _ => new LatencyTracker());
            tracker.Record(microseconds);
        }

        public IEnumerable<MetricRecord> GetAllMetrics()
        {
            foreach (var kvp in _counters)
            {
                yield return new MetricRecord(kvp.Key, MetricType.Counter, kvp.Value.Get());
            }

            foreach (var kvp in _gauges)
            {
                yield return new MetricRecord(kvp.Key, MetricType.Gauge, kvp.Value);
            }

            foreach (var kvp in _latencyTrackers)
            {
                var stats = kvp.Value.GetStats();
                yield return new MetricRecord($"{kvp.Key}_p50", MetricType.Gauge, stats.P50);
                yield return new MetricRecord($"{kvp.Key}_p99", MetricType.Gauge, stats.P99);
                yield return new MetricRecord($"{kvp.Key}_avg", MetricType.Gauge, stats.Avg);
            }
        }

        private sealed class LatencyTracker
        {
            private readonly ConcurrentQueue<double> _samples = new();
            private const int MAX_SAMPLES = 1000;

            public void Record(double val)
            {
                _samples.Enqueue(val);
                while (_samples.Count > MAX_SAMPLES)
                {
                    _samples.TryDequeue(out _);
                }
            }

            public LatencyStats GetStats()
            {
                var currentSamples = _samples.ToList();
                if (currentSamples.Count == 0) return default;
                
                currentSamples.Sort();
                return new LatencyStats
                {
                    P50 = currentSamples[currentSamples.Count / 2],
                    P99 = currentSamples[(int)(currentSamples.Count * 0.99)],
                    Avg = currentSamples.Average()
                };
            }
        }

        private struct LatencyStats
        {
            public double P50;
            public double P99;
            public double Avg;
        }
    }

    public enum MetricType
    {
        Counter,
        Gauge
    }

    public record struct MetricRecord(string Name, MetricType Type, double Value);
}
