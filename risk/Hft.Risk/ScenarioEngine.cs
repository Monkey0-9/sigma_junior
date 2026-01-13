using System;
using System.Collections.Generic;
using System.Linq;
using Hft.Core;

namespace Hft.Risk
{
    /// <summary>
    /// Defines a stress test scenario with predefined market shocks.
    /// GRANDMASTER: Uses init-only properties for immutability.
    /// </summary>
    public readonly struct ShockScenario : IEquatable<ShockScenario>
    {
        public string Name { get; init; }
        public double EquityShock { get; init; }      // % change
        public double VolatilityShock { get; init; }  // bps change
        public double CryptoShock { get; init; }      // % change

        public readonly bool Equals(ShockScenario other) =>
            Name == other.Name && EquityShock == other.EquityShock &&
            VolatilityShock == other.VolatilityShock && CryptoShock == other.CryptoShock;

        public readonly override bool Equals(object? obj) =>
            obj is ShockScenario other && Equals(other);

        public readonly override int GetHashCode() =>
            HashCode.Combine(Name, EquityShock, VolatilityShock, CryptoShock);

        public static bool operator ==(ShockScenario left, ShockScenario right) =>
            left.Equals(right);

        public static bool operator !=(ShockScenario left, ShockScenario right) =>
            !left.Equals(right);
    }

    /// <summary>
    /// Institutional Scenario & Stress Testing Engine.
    /// Replays historical or hypothetical shocks on current portfolio state.
    /// Aligned with Aladdin's scenario analysis framework.
    /// </summary>
    public class ScenarioEngine
    {
        private readonly List<ShockScenario> _predefinedScenarios = new()
        {
            new ShockScenario { Name = "2008 Lehman Crisis", EquityShock = -0.40, VolatilityShock = 500, CryptoShock = -0.60 },
            new ShockScenario { Name = "2020 COVID Crash", EquityShock = -0.30, VolatilityShock = 300, CryptoShock = -0.50 },
            new ShockScenario { Name = "Tech Bubble Burst", EquityShock = -0.50, VolatilityShock = 200, CryptoShock = -0.20 }
        };

        /// <summary>
        /// Computes the projected PnL impact of a specific scenario on a collection of positions.
        /// </summary>
        public Dictionary<string, double> RunStressTests(IEnumerable<PositionSnapshot> positions)
        {
            ArgumentNullException.ThrowIfNull(positions);

            var results = new Dictionary<string, double>();

            foreach (var scenario in _predefinedScenarios)
            {
                double projectedPnL = 0;
                foreach (var pos in positions)
                {
                    double exposure = pos.NetPosition * pos.AvgEntryPrice;

                    // Apply shock based on asset class
                    double shock = pos.AssetClass switch
                    {
                        AssetClass.Equity => scenario.EquityShock,
                        AssetClass.Crypto => scenario.CryptoShock,
                        _ => 0.10 * scenario.EquityShock // Conservative default for others
                    };

                    // Include Factor-based Volatility shock
                    double factorImpact = pos.Factors.Volatility * (scenario.VolatilityShock / 10000.0) * exposure;

                    projectedPnL += (exposure * shock) + factorImpact;
                }
                results[scenario.Name] = projectedPnL;
            }

            return results;
        }
    }
}

