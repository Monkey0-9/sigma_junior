using System.Runtime.InteropServices;

namespace Hft.Core
{
    /// <summary>
    /// Blittable Factor Exposure representation.
    /// Used for systematic risk attribution ( Renaissance / Aladdin ).
    /// Isolated into a struct for L1 cache friendliness.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FactorExposure : IEquatable<FactorExposure>
    {
        public double Beta;      // Market Beta / Equity Risk Premium
        public double Size;      // Small Cap vs Large Cap factor
        public double Value;     // Value vs Growth factor
        public double Momentum;  // Trend following sensitivity
        public double Volatility;// Sensitivity to market turbulence
        public double Liquidity;// Liquidity risk exposure

        public static FactorExposure Neutral() => new FactorExposure();

        public readonly bool Equals(FactorExposure other) =>
            Beta == other.Beta && Size == other.Size && Value == other.Value &&
            Momentum == other.Momentum && Volatility == other.Volatility &&
            Liquidity == other.Liquidity;

        public readonly override bool Equals(object? obj) =>
            obj is FactorExposure other && Equals(other);

        public readonly override int GetHashCode() =>
            HashCode.Combine(Beta, Size, Value, Momentum, Volatility, Liquidity);

        public static bool operator ==(FactorExposure left, FactorExposure right) =>
            left.Equals(right);

        public static bool operator !=(FactorExposure left, FactorExposure right) =>
            !left.Equals(right);
    }
}
