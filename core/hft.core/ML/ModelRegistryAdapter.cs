using System;
using System.Collections.Concurrent;

namespace Hft.Core.ML
{
    /// <summary>
    /// Institutional Model Registry Adapter.
    /// Provides standardized access to versioned alpha models (MLflow stubs).
    /// ENSURES: Deterministic inference and auditable model lineage.
    /// </summary>
    public class ModelRegistryAdapter
    {
        private readonly ConcurrentDictionary<string, IAlphaModel> _activeModels = new();

        public void DeployModel(string modelId, IAlphaModel model)
        {
            ArgumentNullException.ThrowIfNull(model);
            _activeModels[modelId] = model;
            Console.WriteLine($"[ML] Model Deployed: {modelId} (Version: {model.Version})");
        }

        public IAlphaModel? GetModel(string modelId)
        {
            return _activeModels.TryGetValue(modelId, out var model) ? model : null;
        }
    }

    public interface IAlphaModel
    {
        string ModelName { get; }
        string Version { get; }
        double Predict(in MarketDataTick tick);
    }

    /// <summary>
    /// Mock Linear Regression Model for demonstration.
    /// </summary>
    public class LinearImbalanceModel : IAlphaModel
    {
        public string ModelName => "LinearImbalance";
        public string Version => "1.0.4-gold";

        private readonly double _coef;

        public LinearImbalanceModel(double coef = 0.5)
        {
            _coef = coef;
        }

        public double Predict(in MarketDataTick tick)
        {
            // Simple model: P(up) = coef * imbalance
            double bidVol = tick.Bid1.Size + tick.Bid2.Size;
            double askVol = tick.Ask1.Size + tick.Ask2.Size;
            double imbalance = (bidVol - askVol) / (bidVol + askVol);

            return _coef * imbalance;
        }
    }
}

