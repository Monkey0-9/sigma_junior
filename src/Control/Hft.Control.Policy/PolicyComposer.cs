using System;
using System.Collections.Generic;
using System.Linq;

namespace Hft.Control.Policy
{
    /// <summary>
    /// Layer 6: Meta-Alpha & Policy Composer.
    /// Bayesian Model Averaging (BMA) and Alpha Darwinism.
    /// </summary>
    public class PolicyComposer
    {
        private readonly Dictionary<string, double> _modelWeights = new();

        public void RebalanceWeights(List<ModelPerformance> history)
        {
            // Simple Bayesian update for weights
            // w_i = P(Model_i | Data) propto P(Data | Model_i) * P(Model_i)
            double sumWeights = 0;
            foreach (var performance in history)
            {
                double likelihood = Math.Exp(performance.LogLikelihood);
                _modelWeights[performance.ModelId] = likelihood * _modelWeights.GetValueOrDefault(performance.ModelId, 1.0);
                sumWeights += _modelWeights[performance.ModelId];
            }

            // Normalize
            foreach (var id in _modelWeights.Keys.ToList()) _modelWeights[id] /= sumWeights;
        }

        public double ComposePolicy(Dictionary<string, double> modelOutputs)
        {
            return modelOutputs.Sum(x => x.Value * _modelWeights.GetValueOrDefault(x.Key, 0.0));
        }
    }

    public record ModelPerformance(string ModelId, double LogLikelihood);
}
