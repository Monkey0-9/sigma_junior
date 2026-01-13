# FIOS: Digital Twin & World Model API

## 1. Liquidity Density Field (PDE)
Access to the market state at the PDE level.

```csharp
public interface ILiquidityTwin
{
    // Returns rho(x,t)
    double GetDensityAtPrice(double price);
    
    // Injects S(x,t) - Exogenous liquidity shock
    void InjectShock(double priceLevel, double magnitude);
}
```

## 2. Market Foundation Model (SDE)
Scenario generation interface for policy composition.

```csharp
public interface IMarketForecaster
{
    // Returns full scenario distribution p(x_t+1 | x_t)
    ScenarioDistribution PredictDistribution(long horizonMs);
}
```

## 3. Causal Attribution (Explainability)
Counterfactual analysis interface for self-audit.

```csharp
public interface IAttributor
{
    // "What if the trade at decisionId was withheld?"
    double EstimateCounterfactual(long decisionId, string scenario);
}
```
## 4. Formal Constraints (Liveness & Stability)
Access to safety proof registry.

```csharp
public interface IGovernanceGate
{
    // Returns Proof carrying decision safety
    Proof ValidateSafety(DecisionRequest request);
}
```
