namespace Hft.Core
{
    /// <summary>
    /// Regulatory and safety risk limits.
    /// Owned by Core for solution-wide visibility.
    /// </summary>
    public struct RiskLimits
    {
        public double MaxOrderQty;
        public double MaxPosition;
        public int MaxOrdersPerSec;
        public double MaxNotionalPerOrder;
        public bool KillSwitchActive;
    }
}
