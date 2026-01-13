using System;
using Hft.Core;

namespace Hft.Execution.Models;

/// <summary>
/// Gaussian latency model with jitter/spikes.
/// Simulates normal network variability plus occasional outliers.
/// </summary>
public sealed class GaussianLatencyModel : ILatencyModel
{
    private readonly IRandomProvider _random;
    private readonly double _meanLatencyUs;
    private readonly double _stdDevUs;
    private readonly double _spikeProb;
    private readonly double _spikeMultiplier;

    public GaussianLatencyModel(
        IRandomProvider random, 
        double meanLatencyUs = 50.0, 
        double stdDevUs = 5.0,
        double spikeProb = 0.001,
        double spikeMultiplier = 10.0)
    {
        _random = random;
        _meanLatencyUs = meanLatencyUs;
        _stdDevUs = stdDevUs;
        _spikeProb = spikeProb;
        _spikeMultiplier = spikeMultiplier;
    }

    /// <inheritdoc/>
    public double GenerateLatency()
    {
        // Box-Muller transform for normal distribution
        double u1 = 1.0 - _random.NextDouble();
        double u2 = 1.0 - _random.NextDouble();
        double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        
        double latency = _meanLatencyUs + (_stdDevUs * z);
        
        // Ensure non-negative
        latency = Math.Max(1.0, latency);
        
        // Add spikes
        if (_random.NextDouble() < _spikeProb)
        {
            latency *= _spikeMultiplier;
        }
        
        return latency;
    }

    /// <inheritdoc/>
    public double GenerateAckLatency()
    {
        // Acks might be slightly faster or simpler
        return GenerateLatency() * 0.9;
    }
}
