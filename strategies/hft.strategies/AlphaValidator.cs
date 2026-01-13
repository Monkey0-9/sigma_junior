using System;
using System.Collections.Generic;
using System.Linq;

namespace Hft.Strategies
{
    /// <summary>
    /// Institutional Alpha Validator.
    /// Performs statistical checks on alpha performance to ensure validity.
    /// Aligned with Aladdin's signal validation framework.
    /// </summary>
    public static class AlphaValidator
    {
        /// <summary>
        /// Validates a signal's Information Coefficient (IC).
        /// IC is the correlation between predicted returns and actual returns.
        /// </summary>
        public static bool ValidateIC(IEnumerable<double> predictions, IEnumerable<double> outcomes, double threshold = 0.02)
        {
            var pList = predictions.ToList();
            var oList = outcomes.ToList();

            if (pList.Count < 100 || pList.Count != oList.Count) return false;

            double ic = ComputeCorrelation(pList, oList);
            return ic > threshold;
        }

        /// <summary>
        /// Validates that a signal does not have excessive turnover.
        /// </summary>
        public static bool ValidateTurnover(IEnumerable<double> signals, double maxTurnover)
        {
            var sList = signals.ToList();
            if (sList.Count < 2) return true;

            double totalTurnover = 0;
            for (int i = 1; i < sList.Count; i++)
            {
                totalTurnover += Math.Abs(sList[i] - sList[i - 1]);
            }

            double avgTurnover = totalTurnover / (sList.Count - 1);
            return avgTurnover <= maxTurnover;
        }

        private static double ComputeCorrelation(List<double> x, List<double> y)
        {
            int n = x.Count;
            double avgX = x.Average();
            double avgY = y.Average();

            double sumXY = 0;
            double sumX2 = 0;
            double sumY2 = 0;

            for (int i = 0; i < n; i++)
            {
                double dx = x[i] - avgX;
                double dy = y[i] - avgY;
                sumXY += dx * dy;
                sumX2 += dx * dx;
                sumY2 += dy * dy;
            }

            double den = Math.Sqrt(sumX2 * sumY2);
            return den == 0 ? 0 : sumXY / den;
        }
    }
}
