using System;
using System.Collections.Generic;

namespace Hft.Execution
{
    /// <summary>
    /// Parameters for the Almgren-Chriss optimization.
    /// </summary>
    public record AlmgrenChrissParameters(
        double TotalQuantity,      // X: Total number of shares to trade
        double TotalTimeMinutes,   // T: Total time allowed for execution
        int Intervals,             // N: Number of discrete time intervals
        double VolatilityMinutes,  // sigma: Volatility per minute
        double RiskAversion,       // lambda: Risk aversion parameter
        double TempImpactCoeff,    // eta: Temporary impact coefficient
        double PermImpactCoeff     // gamma: Permanent impact coefficient
    );

    /// <summary>
    /// Implementation of the Almgren-Chriss optimal execution model.
    /// Calculates the optimal trading trajectory to minimize the sum of 
    /// transaction costs (market impact) and volatility risk.
    /// </summary>
    public sealed class AlmgrenChrissOptimizer
    {
        /// <summary>
        /// Calculates the optimal schedule (number of shares to trade in each interval).
        /// </summary>
        public static double[] CalculateOptimalSchedule(AlmgrenChrissParameters p)
        {
            ArgumentNullException.ThrowIfNull(p);
            if (p.Intervals <= 0) throw new ArgumentException("Intervals must be > 0", nameof(p));
            
            double tau = p.TotalTimeMinutes / p.Intervals; // Time per interval
            
            // kappa^2 = (lambda * sigma^2) / eta_tilde
            // where eta_tilde = eta / tau (impact adjusted for interval duration)
            // Simplified Almgren-Chriss kappa:
            double etaTilde = p.TempImpactCoeff / tau;
            double kappaSquared = (p.RiskAversion * p.VolatilityMinutes * p.VolatilityMinutes) / p.TempImpactCoeff;
            double kappa = Math.Sqrt(kappaSquared);

            double[] schedule = new double[p.Intervals];
            double totalTraded = 0;

            for (int k = 1; k <= p.Intervals; k++)
            {
                // Optimal number of shares n_k in interval k:
                // n_k = 2 * sinh(1/2 * kappa * tau) / sinh(kappa * T) * cosh(kappa * (T - (k - 1/2) * tau)) * X
                
                double term1 = 2 * Math.Sinh(0.5 * kappa * tau) / Math.Sinh(kappa * p.TotalTimeMinutes);
                double term2 = Math.Cosh(kappa * (p.TotalTimeMinutes - (k - 0.5) * tau));
                
                schedule[k - 1] = term1 * term2 * p.TotalQuantity;
                totalTraded += schedule[k - 1];
            }

            // Normalization to ensure total quantity is met (offsets floating point errors)
            double adjustment = p.TotalQuantity / totalTraded;
            for (int i = 0; i < schedule.Length; i++)
            {
                schedule[i] *= adjustment;
            }

            return schedule;
        }

        /// <summary>
        /// Calculates the expected cost (shortfall) of the optimal trajectory.
        /// </summary>
        public static double CalculateExpectedShortfall(AlmgrenChrissParameters p, double[] schedule)
        {
            ArgumentNullException.ThrowIfNull(p);
            ArgumentNullException.ThrowIfNull(schedule);
            double tau = p.TotalTimeMinutes / p.Intervals;
            double shortfall = 0;

            // Permanent impact: 1/2 * gamma * X^2
            shortfall += 0.5 * p.PermImpactCoeff * p.TotalQuantity * p.TotalQuantity;

            // Temporary impact: sum(eta_tilde * n_k^2)
            double etaTilde = p.TempImpactCoeff / tau;
            foreach (double nk in schedule)
            {
                shortfall += etaTilde * nk * nk;
            }

            return shortfall;
        }
    }
}
