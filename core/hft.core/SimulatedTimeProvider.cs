using System;

namespace Hft.Core;

/// <summary>
/// Simulated time provider with controllable time progression.
/// GRANDMASTER: Use for simulations, backtests, and replay scenarios.
/// Enables deterministic time control and time-travel debugging.
/// </summary>
public sealed class SimulatedTimeProvider : ITimeProvider
{
    private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private DateTime _currentTime;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance starting at the specified time.
    /// </summary>
    /// <param name="startTime">The initial simulated time.</param>
    public SimulatedTimeProvider(DateTime startTime)
    {
        _currentTime = startTime.ToUniversalTime();
    }

    /// <summary>
    /// Initializes a new instance starting at the current system time.
    /// </summary>
    public SimulatedTimeProvider() : this(DateTime.UtcNow)
    {
    }

    /// <inheritdoc/>
    public DateTime UtcNow
    {
        get
        {
            lock (_lock)
                return _currentTime;
        }
    }

    /// <inheritdoc/>
    public DateTimeOffset Now
    {
        get
        {
            lock (_lock)
                return new DateTimeOffset(_currentTime);
        }
    }

    /// <inheritdoc/>
    public long EpochMicroseconds
    {
        get
        {
            lock (_lock)
            {
                var elapsed = _currentTime - UnixEpoch;
                return (long)(elapsed.TotalMilliseconds * 1000);
            }
        }
    }

    /// <summary>
    /// Advances the simulated time by the specified duration.
    /// </summary>
    /// <param name="duration">The time span to advance.</param>
    public void Advance(TimeSpan duration)
    {
        lock (_lock)
        {
            _currentTime = _currentTime.Add(duration);
        }
    }

    /// <summary>
    /// Sets the simulated time to a specific value.
    /// </summary>
    /// <param name="time">The new simulated time.</param>
    public void SetTime(DateTime time)
    {
        lock (_lock)
        {
            _currentTime = time.ToUniversalTime();
        }
    }
}
