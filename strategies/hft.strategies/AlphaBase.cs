using System;
using Hft.Core;

namespace Hft.Strategies
{
    /// <summary>
    /// Institutional Alpha Base Class.
    /// Provides standardized lifecycle and governance for alpha models.
    /// ENSURES: Separation of concerns between alpha generation and execution.
    /// </summary>
    public abstract class AlphaBase
    {
        public string Name { get; }
        public string Version { get; }
        public bool IsEnabled { get; protected set; } = true;

        protected AlphaBase(string name, string version)
        {
            Name = name;
            Version = version;
        }

        /// <summary>
        /// Produces a signal for a given market tick.
        /// Result should be normalized (e.g., [-1, 1]).
        /// </summary>
        public abstract double GenerateSignal(in MarketDataTick tick);

        /// <summary>
        /// Called when the alpha governance layer decides to disable this alpha.
        /// </summary>
        public virtual void Disable(string reason)
        {
            IsEnabled = false;
            Console.WriteLine($"[GOVERNANCE] Alpha {Name} (v{Version}) DISABLED: {reason}");
        }
    }
}
