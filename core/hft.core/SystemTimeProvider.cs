using System;

namespace Hft.Core;

/// <summary>
/// Production time provider using the system clock.
/// GRANDMASTER: Use for live trading and real-time scenarios.
/// </summary>
public sealed class SystemTimeProvider : ITimeProvider
{
    private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc/>
    public DateTime UtcNow => DateTime.UtcNow;

    /// <inheritdoc/>
    public DateTimeOffset Now => DateTimeOffset.Now;

    /// <inheritdoc/>
    public long EpochMicroseconds
    {
        get
        {
            var elapsed = DateTime.UtcNow - UnixEpoch;
            return (long)(elapsed.TotalMilliseconds * 1000);
        }
    }
}
