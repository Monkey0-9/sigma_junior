using System.Threading;

namespace Hft.Core
{
    /// <summary>
    /// GRANDMASTER: Sealed class for performance and security (CA1052).
    /// </summary>
    public sealed class MetricsCounter
    {
        private long _value;
        public string Name { get; }

        public MetricsCounter(string name)
        {
            Name = name;
        }

        public void Increment(long amount = 1)
        {
            Interlocked.Add(ref _value, amount);
        }

        public long Get() => Interlocked.Read(ref _value);
    }
}
