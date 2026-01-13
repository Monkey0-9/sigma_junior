using System;
using System.Security.Cryptography;

namespace Hft.Core;

/// <summary>
/// Cryptographically secure random number provider.
/// GRANDMASTER: Use for production scenarios requiring cryptographic-grade randomness.
/// NOT suitable for simulations requiring deterministic replay.
/// </summary>
public sealed class CryptoRandomProvider : IRandomProvider, IDisposable
{
    private readonly RandomNumberGenerator _rng;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance using the system's cryptographic RNG.
    /// </summary>
    public CryptoRandomProvider()
    {
        _rng = RandomNumberGenerator.Create();
    }

    /// <inheritdoc/>
    public int Next()
    {
        return Next(0, int.MaxValue);
    }

    /// <inheritdoc/>
    public int Next(int maxValue)
    {
        if (maxValue < 0)
            throw new ArgumentOutOfRangeException(nameof(maxValue), "maxValue must be non-negative");
        
        return Next(0, maxValue);
    }

    /// <inheritdoc/>
    public int Next(int minValue, int maxValue)
    {
        if (minValue > maxValue)
            throw new ArgumentOutOfRangeException(nameof(minValue), "minValue must be less than or equal to maxValue");

        if (minValue == maxValue)
            return minValue;

        long range = (long)maxValue - minValue;
        byte[] bytes = new byte[4];
        _rng.GetBytes(bytes);
        uint randomValue = BitConverter.ToUInt32(bytes, 0);
        return (int)(minValue + (randomValue % range));
    }

    /// <inheritdoc/>
    public double NextDouble()
    {
        byte[] bytes = new byte[8];
        _rng.GetBytes(bytes);
        ulong randomValue = BitConverter.ToUInt64(bytes, 0);
        // Convert to [0.0, 1.0) by dividing by max ulong + 1
        return randomValue / (double)(ulong.MaxValue);
    }

    /// <inheritdoc/>
    public void NextBytes(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _rng.GetBytes(buffer);
    }

    /// <summary>
    /// Releases resources used by the cryptographic RNG.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rng.Dispose();
        GC.SuppressFinalize(this);
    }
}
