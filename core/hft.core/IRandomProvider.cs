namespace Hft.Core;

/// <summary>
/// Abstraction for random number generation.
/// Enables deterministic behavior in simulations and replay scenarios.
/// GRANDMASTER: All stochastic components must use IRandomProvider for reproducibility.
/// </summary>
#pragma warning disable CA1716 // ARCHITECTURE EXCEPTION: 'Next' matches System.Random API contract for compatibility
public interface IRandomProvider
{
    /// <summary>
    /// Returns a non-negative random integer.
    /// </summary>
    int Next();

    /// <summary>
    /// Returns a non-negative random integer less than the specified maximum.
    /// </summary>
    int Next(int maxValue);

    /// <summary>
    /// Returns a random integer within a specified range.
    /// </summary>
    int Next(int minValue, int maxValue);

    /// <summary>
    /// Returns a random floating-point number between 0.0 and 1.0.
    /// </summary>
    double NextDouble();

    /// <summary>
    /// Fills the elements of a specified array of bytes with random numbers.
    /// </summary>
    void NextBytes(byte[] buffer);
}
#pragma warning restore CA1716

