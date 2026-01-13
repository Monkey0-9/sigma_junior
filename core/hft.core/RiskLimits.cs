using System.Collections.Generic;

namespace Hft.Core
{
    /// <summary>
    /// Institutional Risk Management Configuration.
    /// Hierarchical limits for Symbol, Strategy, and global Session.
    /// GRANDMASTER: Sealed class for performance and security (CA1052).
    /// </summary>
    public sealed class RiskLimits
    {
        // Global Limits
        public double MaxOrderQty { get; set; } = 1000;
        public double MaxPosition { get; set; } = 5000;
        public int MaxOrdersPerSec { get; set; } = 100;
        public double MaxNotionalPerOrder { get; set; } = 100000;
        public bool KillSwitchActive { get; set; }

        // Capital Protection
        public double DailyLossLimit { get; set; } = 50000;
        public double MaxDrawdownLimit { get; set; } = 10000;
        
        // Volatility Throttling
        public double MaxVolatilityBps { get; set; } = 50; // 0.5% price move in short window

        // Symbol Overrides
        public IReadOnlyDictionary<long, SymbolLimit> SymbolOverrides { get; set; } = new Dictionary<long, SymbolLimit>();
    }

    /// <summary>
    /// GRANDMASTER: Sealed class for performance and security (CA1052).
    /// </summary>
    public sealed class SymbolLimit
    {
        public double MaxOrderQty { get; set; }
        public double MaxPosition { get; set; }
        public double MaxNotionalPerOrder { get; set; }
    }
}

