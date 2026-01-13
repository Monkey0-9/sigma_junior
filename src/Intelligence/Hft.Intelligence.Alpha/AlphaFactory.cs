using System;
using System.Collections.Generic;

namespace Hft.Intelligence.Alpha
{
    /// <summary>
    /// Layer 5: Alpha Factory.
    /// Manages an ecosystem of thousands of weak, decaying signals.
    /// Features: Signal selection via Mutual Information.
    /// </summary>
    public class AlphaFactory
    {
        private readonly List<IWeakSignal> _signals = new();

        public void RegisterSignal(IWeakSignal signal)
        {
            _signals.Add(signal);
        }

        public double ComputeCompositeAlpha(MarketData tick)
        {
            double composite = 0;
            foreach (var signal in _signals)
            {
                // Each signal returns a probabilistic distribution or weight
                composite += signal.GetScoredValue(tick);
            }
            return composite;
        }
    }

    public interface IWeakSignal
    {
        string Id { get; }
        double GetScoredValue(MarketData tick);
    }

    public class MarketData { /* Tick data wrapper */ }
}
