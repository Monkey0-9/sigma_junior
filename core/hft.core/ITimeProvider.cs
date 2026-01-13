using System;

namespace Hft.Core;

/// <summary>
/// Abstraction for time access.
/// Enables deterministic time control in simulations and replay scenarios.
/// GRANDMASTER: All time-dependent components must use ITimeProvider for reproducibility.
/// </summary>
public interface ITimeProvider
{
    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Gets the current time with offset.
    /// </summary>
    DateTimeOffset Now { get; }

    /// <summary>
    /// Gets the current time in microseconds since Unix epoch.
    /// </summary>
    long EpochMicroseconds { get; }
}
