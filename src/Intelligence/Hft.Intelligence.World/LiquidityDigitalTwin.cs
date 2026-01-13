using System;

namespace Hft.Intelligence.World
{
    /// <summary>
    /// Layer 4: World Model / Digital Twin.
    /// Simulates market as a coupled PDE system.
    /// Equation: d_rho/dt + grad(rho * v) = S(x,t)
    /// </summary>
    public class LiquidityDigitalTwin
    {
        private const double GridSize = 100;
        private readonly double[] _densityField = new double[(int)GridSize];

        public void SolveStep(double dt, double priceVelocity, double exogenousShock)
        {
            // Simplified Finite Difference scheme for the Continuity Equation
            for (int i = 1; i < GridSize - 1; i++)
            {
                double flux = _densityField[i] * priceVelocity;
                double divergence = (flux - (_densityField[i-1] * priceVelocity)) / 1.0;
                
                _densityField[i] += dt * (-divergence + ExogenousSource(i, exogenousShock));
            }
        }

        private double ExogenousSource(int gridIndex, double shock)
        {
            // S(x,t) - Source/Sink term (e.g., hidden liquidity entry)
            return Math.Exp(-Math.Pow(gridIndex - (GridSize / 2), 2)) * shock;
        }

        public double GetLiquidityAtPrice(double price)
        {
            int index = (int)Math.Clamp(price, 0, GridSize - 1);
            return _densityField[index];
        }
    }
}
