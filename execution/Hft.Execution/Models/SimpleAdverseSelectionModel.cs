using System;
using Hft.Core;

namespace Hft.Execution.Models;

/// <summary>
/// Simple adverse selection model.
/// Assumes larger trades attract more toxic flow / information leakage.
/// </summary>
public sealed class SimpleAdverseSelectionModel : IAdverseSelectionModel
{
    private readonly IRandomProvider _random;
    private readonly double _toxicityFactor;

    public SimpleAdverseSelectionModel(IRandomProvider random, double toxicityFactor = 0.5)
    {
        _random = random;
        _toxicityFactor = toxicityFactor; 
    }

    /// <inheritdoc/>
    public double CalculatePostTradeMoves(double tradeSize, double volatility)
    {
        // Adverse selection: Price moves X standard deviations against you
        // correlated with trade size.
        
        // Log-linear toxicity: larger trades leak more info
        double sizeImpact = Math.Log10(Math.Max(10, tradeSize)); 
        
        // Stochastic component: heavily skewed (fat tail risk)
        double riskRoll = _random.NextDouble();
        double toxicityMultiplier = riskRoll > 0.9 ? 2.0 : 1.0; 
        
        return volatility * sizeImpact * _toxicityFactor * toxicityMultiplier;
    }
}
