# Institutional Hardening - Implementation TODO

## Phase 1: Engineering Hardening (Zero-Warning Build)

### 1.1 CA1051 - Visible Instance Fields

#### PositionSnapshot.cs
- [ ] Make NetPosition, AvgEntryPrice, RealizedPnL, UnrealizedPnL private
- [ ] Add public properties with volatile accessors
- [ ] Update all references in calling code

#### FeatureCrossValidator.cs
- [ ] Make all visible instance fields private
- [ ] Add properties if needed

#### ScenarioEngine.cs
- [ ] Make all visible instance fields private
- [ ] Add properties if needed

### 1.2 CA1510 - ArgumentNullException.ThrowIfNull

#### ModelRegistryAdapter.cs
- [ ] Line 17: Replace `throw new ArgumentNullException()` with `ArgumentNullException.ThrowIfNull()`

#### Primitives.cs
- [ ] Line 167: Replace `throw new ArgumentNullException()` 
- [ ] Line 168: Replace `throw new ArgumentNullException()`

#### CrossCloudSyncProvider.cs
- [ ] Line 30: Replace `throw new ArgumentNullException()`

#### ScenarioEngine.cs
- [ ] Line 54: Replace `throw new ArgumentNullException()`

#### PortfolioRiskEngine.cs
- [ ] Line 27: Replace `throw new ArgumentNullException()`

### 1.3 CS9191 - ref vs in Modifier

#### AppendOnlyLog.cs
- [ ] Line 43: Fix ref to in modifier pattern

### 1.4 CA1303 - Literal String Parameters

#### MetricsServer.cs
- [ ] Line 57: Use composite format or string resources
- [ ] Line 64: Use composite format or string resources

#### TickReplay.cs
- [ ] Line 66: Use composite format or string resources

### 1.5 CA1822 - Static Member

#### FeatureCrossValidator.cs
- [ ] Line 46: Mark `ValidateSignal` as static

---

## Phase 2: Application Lifecycle Management

- [ ] Create EngineState enum with all lifecycle states
- [ ] Implement formal lifecycle controller in TradingEngine
- [ ] Add graceful shutdown handlers for Ctrl+C and SIGTERM
- [ ] Ensure no resource leaks during shutdown

## Phase 3: Risk Dominance

- [ ] Implement three-layer architecture (Strategy → Risk → Execution)
- [ ] Refactor PreTradeRiskEngine for hard limits enforcement
- [ ] Implement KillSwitch as IDisposable
- [ ] Add portfolio-level risk checks

## Phase 4: Audit & Governance

- [ ] Enhance AppendOnlyLog with proper HMAC chain
- [ ] Create audit event schema
- [ ] Implement deterministic replay engine
- [ ] Add cryptographic integrity verification

## Phase 5: Alpha Governance

- [ ] Create AlphaBase abstract class
- [ ] Implement AlphaValidator for statistical checks
- [ ] Enhance AlphaDecayMonitor with IC tracking
- [ ] Implement automatic alpha disable mechanism

## Phase 6: Execution Realism

- [ ] Enhance OrderBookSimulator with queue modeling
- [ ] Implement partial fill simulation
- [ ] Add slippage estimation

## Phase 7: Metrics & Operations

- [ ] Enhance MetricsServer for Prometheus format
- [ ] Add histogram metrics for latency tracking
- [ ] Implement alerting rules

## Phase 8: CI/CD & Validation

- [ ] Create azure-pipelines.yml
- [ ] Create GitHub Actions workflow
- [ ] Add deterministic replay validation to CI
- [ ] Configure warning-as-error in CI

---

## Verification Commands

```bash
# Phase 1 Verification
dotnet clean
dotnet build -c Release
# Expected: 0 errors, 0 warnings

# Full Build Verification
dotnet build
# Expected: All projects build successfully

# Run Tests
dotnet test
# Expected: All tests pass
```

## Progress Tracking

| Phase | Status | Warnings Remaining | Notes |
|-------|--------|-------------------|-------|
| Phase 1 | Not Started | 24 | TBD |
| Phase 2 | Not Started | - | After Phase 1 |
| Phase 3 | Not Started | - | After Phase 2 |
| Phase 4 | Not Started | - | After Phase 3 |
| Phase 5 | Not Started | - | After Phase 4 |
| Phase 6 | Not Started | - | After Phase 5 |
| Phase 7 | Not Started | - | After Phase 6 |
| Phase 8 | Not Started | - | After Phase 7 |

