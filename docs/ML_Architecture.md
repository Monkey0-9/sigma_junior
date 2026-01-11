# Institutional ML Alpha Architecture (Post-Infrastructure)

## 1. High-Level Architecture

The architecture enforces a strict separation between **Offline Training** and **Online Inference**, bridged by a **Model Registry** and **Feature Governance** policy.

### Diagram: Offline vs Online Flow

```mermaid
graph TD
    subgraph Offline [Offline Training Pipeline (Python)]
        RawData[Raw Market Data (CSV/Parquet)] --> FeatureEnginePy[Feature Engine (Python)]
        FeatureEnginePy --> FeatureStore[Feature Snapshot (Parquet)]
        FeatureStore --> Trainer[Model Trainer (Sklearn/PyTorch)]
        Trainer --> Audit[Audit & Validation Checks]
        Audit --> Registry[Model Registry (JSON + Artifacts)]
    end

    subgraph Online [Online Inference Pipeline (C#)]
        MarketData[Market Data Feed] --> RingBuffer[Ring Buffer]
        RingBuffer --> FeatureCalcCS[Feature Calculator (C#)]
        FeatureCalcCS --> Inference[ONNX Inference Engine]
        Registry --> ModelLoader[Model Loader (Hot Reload)]
        ModelLoader --> Inference
        Inference --> Signal[Alpha Signal]
        Signal --> Strategy[Strategy & Risk]
    end
```

## 2. Feature Governance & Parity

To prevent training-serving skew, features are defined logically in a shared schema but implemented twice with strict regression tests:

1. **Python (Pandas/Polars)**: Vectorized, batch-mode for training.
2. **C# (Zero-Alloc)**: Incremental, tick-by-tick for inference.

**Lineage Rule**: Every model version in the registry points to a specific Git commit hash of the Feature Definition.

## 3. Component Interfaces

### 3.1 Model Registry Schema (`ml/registry.json`)

```json
{
  "models": [
    {
      "id": "momentum_v1_001",
      "version": 1,
      "status": "production",  // candidate, production, archived
      "path": "ml/models/momentum_v1_001.onnx",
      "sha256": "a3f8...",
      "input_features": ["bid_ask_spread", "mid_price_velocity_1s"],
      "output_schema": ["probability_up", "probability_down"],
      "created_at": "2026-01-11T12:00:00Z",
      "owner": "quant_team",
      "training_data_snapshot": "data/snapshots/20260110_full.parquet"
    }
  ]
}
```

### 3.2 Feature Calculator (C#)

```csharp
public interface IFeatureCalculator {
    void OnTick(MarketDataTick tick);
    // Writes directly to pre-allocated tensor buffer
    void FillFeatureVector(Span<float> buffer);
}
```

### 3.3 Inference Engine (C#)

- Uses `Microsoft.ML.OnnxRuntime`.
- **Latency Strategy**:
  - Pre-allocated `OrtValue` tensors (recycle memory).
  - SessionOptions: `ExecutionMode.Sequential`, `GraphOptimizationLevel.ORT_ENABLE_ALL`.
  - Thread pinning if possible.

## 4. Safety & Governance Logic

1. **Advisory Only**: The ML Strategy outputs a "Suggestion" (Buy/Sell + Confidence). The Risk Engine (existing) validates limits independently.
2. **Confidence Threshold**: `if (confidence < MinThreshold) return NoSignal;`
3. **Approve/Block**: `RiskLimits` extended to include `MaxModelExposure` per model ID.
