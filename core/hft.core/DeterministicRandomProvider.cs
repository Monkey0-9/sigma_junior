using System;

namespace Hft.Core;

/// <summary>
/// Deterministic random number provider using a seeded System.Random.
/// GRANDMASTER: Use for simulations, backtests, and replay scenarios.
/// Guarantees reproducible results given the same seed.
/// </summary>
#pragma warning disable CA5394 // ARCHITECTURE EXCEPTION: System.Random is intentionally used for deterministic replay. NOT for cryptographic purposes.
public sealed class DeterministicRandomProvider : IRandomProvider
{
    private readonly Random _rng;

    /// <summary>
    /// The seed used to initialize this provider.
    /// </summary>
    public int Seed { get; }

    /// <summary>
    /// Initializes a new instance with the specified seed.
    /// </summary>
    /// <param name="seed">The seed value for reproducible random sequences.</param>
    public DeterministicRandomProvider(int seed)
    {
        Seed = seed;
        _rng = new Random(seed);
    }

    /// <inheritdoc/>
    public int Next() => _rng.Next();

    /// <inheritdoc/>
    public int Next(int maxValue) => _rng.Next(maxValue);

    /// <inheritdoc/>
    public int Next(int minValue, int maxValue) => _rng.Next(minValue, maxValue);

    /// <inheritdoc/>
    public double NextDouble() => _rng.NextDouble();

    /// <inheritdoc/>
    public void NextBytes(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _rng.NextBytes(buffer);
    }
}
#pragma warning restore CA5394

