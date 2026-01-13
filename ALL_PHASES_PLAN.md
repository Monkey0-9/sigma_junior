# HFT Platform - Master Implementation Plan

## Current Build Status
**FAILED** - 7 errors in infra/hft.infra/ReplayEngine.cs:
1. Missing `using Hft.Core.Audit;` directive
2. CA1822: `ValidateDeterminism` should be static

---

## Phase 1: Critical Build Fixes (IMMEDIATE)

### 1.1 Fix ReplayEngine.cs
- [ ] Add `using Hft.Core.Audit;` 
- [ ] Make `ValidateDeterminism` static

### 1.2 Rebuild Verification
- [ ] Run `dotnet build` to verify zero errors
- [ ] Check for any remaining warnings

---

## Phase 2: CA Warning Fixes (From CA_FIXES_PROGRESS.md)

### 2.1 Security Fixes
- [ ] `UdpMarketDataSimulator.cs:68` - Replace insecure Random with RandomNumberGenerator (CA5394)
- [ ] `PreTradeRiskEngine.cs:9` - Implement IDisposable for _cts (CA1001)

### 2.2 Method to Property Conversions (CA1024)
- [ ] `MultiAssetPnlEngine.GetTotalPortfolioValueUsd()` → `TotalPortfolioValueUsd` property
- [ ] `PortfolioRiskEngine.GetPortfolioExposure()` → `PortfolioExposure` property

### 2.3 Exception Handling (CA1031) - Catch Specific Exceptions
- [ ] `UdpMarketDataListener.Stop()` - Catch specific exceptions
- [ ] `UdpMarketDataListener.RunLoop()` - Catch specific exceptions
- [ ] `UdpMarketDataSimulator.Stop()` - Catch specific exceptions
- [ ] `MetricsServer.Start()` - Catch specific exceptions
- [ ] `MetricsServer.Stop()` - Catch specific exceptions
- [ ] `MetricsServer.RunLoop()` - Catch specific exceptions
- [ ] `CrossCloudSyncProvider.SyncStateAsync()` - Catch specific exceptions

### 2.4 Parameter Modifiers (CS9191)
- [ ] `AppendOnlyLog.Append()` - Use 'in' instead of 'ref' for struct parameter

### 2.5 Remove Default Initialization (CA1805)
- [ ] `FilesystemBootstrap._initialized` - Remove explicit default initialization

### 2.6 Nested Types to File Level (CA1034)
- [ ] `RegulatoryComplianceDashboard.ComplianceMetric` - Move to file level
- [ ] `FeatureCrossValidator.ValidationResult` - Move to file level
- [ ] `ScenarioEngine.ShockScenario` - Move to file level

### 2.7 Collection Type Changes (CA1002)
- [ ] `MetricsServer._counters` - Change List<MetricsCounter> to IReadOnlyList<MetricsCounter>

### 2.8 Localization (CA1303, CA1305)
- [ ] `MetricsServer.Start()` - Use resource strings for literals
- [ ] `TickReplay.Replay()` - Use resource strings for literals
- [ ] `MetricsServer.RunLoop()` - Add IFormatProvider to StringBuilder.AppendLine calls

### 2.9 Stream WriteAsync (CA1835)
- [ ] `MetricsServer.RunLoop()` - Use Stream.WriteAsync(ReadOnlyMemory<byte>, CancellationToken)

### 2.10 Naming Conventions (CA1707, CA1052)
- [ ] `Hft.Benchmarks.Program.cs` - Rename `TryWrite_TryRead` to `TryWriteTryRead`
- [ ] `Hft.Benchmarks.Program.cs` - Make Program class static or sealed

### 2.11 Seal Internal Classes (CA1852)
- [ ] `AlphaDecayMonitor.SignalHistory` - Seal the class

---

## Phase 3: Engineering Hardening (From HARDENING_IMPLEMENTATION_TODO.md)

### 3.1 CA1051 - Visible Instance Fields
- [ ] `PositionSnapshot.cs` - Make public fields private, add properties
- [ ] `FeatureCrossValidator.cs` - Make visible instance fields private
- [ ] `ScenarioEngine.cs` - Make visible instance fields private

### 3.2 CA1510 - ArgumentNullException.ThrowIfNull
- [ ] `ModelRegistryAdapter.cs` - Replace `throw new ArgumentNullException()`
- [ ] `Primitives.cs` - Replace `throw new ArgumentNullException()`
- [ ] `CrossCloudSyncProvider.cs` - Replace `throw new ArgumentNullException()`
- [ ] `ScenarioEngine.cs` - Replace `throw new ArgumentNullException()`
- [ ] `PortfolioRiskEngine.cs` - Replace `throw new ArgumentNullException()`

### 3.3 CA1822 - Static Member
- [ ] `FeatureCrossValidator.cs` - Mark `ValidateSignal` as static

---

## Phase 4: Institutional Fixes (From INSTITUTIONAL_FIXES_TODO.md)

### Phase 4.1: Infrastructure & Configuration
- [ ] Remove global NoWarn suppressions from Directory.Build.props
- [ ] Add strategies project reference to Hft.Runner.csproj

### Phase 4.2: Risk Module (hft.risk)
- [ ] `ScenarioEngine.cs` - Move ShockScenario struct to file level (CA1034)
- [ ] `PreTradeRiskEngine.cs` - Catch specific exceptions (CA1031)

### Phase 4.3: Execution Module (hft.execution)
- [ ] `ExecutionEngine.cs` - Fix CA1001 (IDisposable), CA1031, CA5394 (Random)

### Phase 4.4: Backtest Module
- [ ] `SimpleMatchingEngine.cs` - Fix CA1001 (IDisposable), CA1805, CA1031
- [ ] `BacktestNode.cs` - Fix CA1063/CA1816 (Dispose pattern), CA2213 (dispose fields)

### Phase 4.5: FIOS Module
- [ ] `Hft.FIOS.cs` - Fix CA1815 (DecisionRequest, SystemState), CA5394, CA1823

### Phase 4.6: Runner Module
- [ ] `Program.cs` - Fix CA1307, CA1305, CA1303, CA1031, CA1852
- [ ] `TradingEngine.cs` - Fix CA1063/CA1816, CA1305, CA1837, CA1031, CA2213

---

## Phase 5: Grandmaster Hardening (From GRANDMASTER_FIXES_TODO.md)

### Phase 5.1: Build Infrastructure & Warnings Cleanup
- [x] 1.1 Add TreatWarningsAsErrors to Directory.Build.props
- [x] 1.2 Fix CA1063 dispose patterns in all IDisposable classes
- [x] 1.3 Fix CA1863 string interpolation analyzer warnings
- [x] 1.4 Fix CA2016 and CA5394 randomness warnings (use RandomNumberGenerator)
- [x] 1.5 Initialize Task fields properly (null or CompletedTask)

### Phase 5.2: Domain Primitive Canonicalization
- [x] 2.1 Verify Order, Fill, MarketDataTick are ONLY in Core.Primitives
- [x] 2.2 Add BinaryPrimitives serialization for MarketDataTick (via Marshal)
- [x] 2.3 Add unit test for MarketDataTick marshaling size
- [x] 2.4 Add explicit file share flags in AppendOnlyLog

### Phase 5.3: Lifecycle & Cancellation Safety
- [x] 3.1 Add CancellationToken parameters to Start methods
- [x] 3.2 Implement proper dispose patterns with GC.SuppressFinalize
- [x] 3.3 Add graceful shutdown in Program.Main with Console.CancelKeyPress
- [x] 3.4 Fix Task initialization in UdpMarketDataListener
- [x] 3.5 Fix Task initialization in UdpMarketDataSimulator
- [x] 3.6 Fix Task initialization in ExecutionEngine
- [x] 3.7 Fix Task initialization in PreTradeRiskEngine

### Phase 5.4: Metrics Server Hardening
- [x] 4.1 Default to unprivileged port 9180
- [x] 4.2 Wrap HttpListener.Start() in try/catch with helpful messages
- [x] 4.3 Fix string interpolation CultureInfo usage
- [x] 4.4 Use ReadOnlyMemory<byte> overload for WriteAsync

### Phase 5.5: File System & Audit
- [x] 5.1 Enhance FilesystemBootstrap to create all required dirs
- [x] 5.2 Add explicit FileShare.Read in all FileStream constructors
- [x] 5.3 Ensure AppendOnlyLog handles directory creation failures gracefully
- [x] 5.4 Add replay verification test (in GrandmasterTests.cs)

### Phase 5.6: Order Immutability Verification
- [x] 6.1 Verify Order struct has init-only properties
- [x] 6.2 Verify no code mutates Order after creation
- [x] 6.3 Add Order immutability unit test

### Phase 5.7: Tests & CI
- [x] 7.1 Create domain primitive serialization test
- [x] 7.2 Create Order immutability test
- [x] 7.3 Create MetricsServer graceful failure test
- [x] 7.4 Create AppendOnlyLog create/replay test
- [x] 7.5 Create integration smoke test (in CI pipeline)
- [x] 7.6 Add GitHub Actions CI pipeline

### Phase 5.8: Developer Scripts
- [x] 8.1 Create tools/stop_runner.ps1
- [x] 8.2 Create tools/bootstrap_dev.ps1
- [ ] 8.3 Add appsettings.Development.json with safe defaults (optional)

### Phase 5.9: Documentation
- [x] 9.1 Update ARCHITECTURE.md with Grandmaster rules
- [ ] 9.2 Create rollback plan documentation (skipped)

---

## Phase 6: Optimization (From HFT_OPTIMIZATION_PLAN.md)

### 6.1 Critical Path - Nanosecond-Level Optimizations
- [ ] Replace DateTime.UtcNow.Ticks with Stopwatch.GetTimestamp()
- [ ] Socket Buffer Tuning & Low-Latency Configuration
- [ ] Cache Line Padding (False Sharing Elimination)
- [ ] Volatile to MemoryBarrier Optimization

### 6.2 Data Structure Optimizations
- [ ] Blittable Order Struct optimization
- [ ] Lock-Free Order ID Generation with Padding
- [ ] MarketDataTick Hardware Timestamp Support
- [ ] Fill Struct Optimization

### 6.3 Risk Engine Optimizations
- [ ] Batch Processing for Risk Checks
- [ ] Reduced Memory Allocations
- [ ] Monotonic Timer with Hardware Counter

### 6.4 CPU Affinity & Scheduling
- [ ] Processor Affinity for Critical Threads
- [ ] Priority Elevation

### 6.5 SIMD & Advanced Optimizations
- [ ] SIMD-Optimized Price Calculations
- [ ] Prefetching for Ring Buffer Access

---

## Verification Commands

```bash
# Phase 1 Verification
dotnet build
# Expected: 0 errors

# Phase 2-5 Verification
dotnet build -c Release
# Expected: 0 errors, 0 warnings

# Full Build
dotnet build
# Expected: All projects build successfully

# Run Tests
dotnet test
# Expected: All tests pass
```

---

## Progress Tracking

| Phase | Status | Errors | Warnings | Notes |
|-------|--------|--------|----------|-------|
| Phase 1 | IN PROGRESS | 7 | - | ReplayEngine.cs fixes |
| Phase 2 | PENDING | - | TBD | CA warnings |
| Phase 3 | PENDING | - | TBD | Engineering hardening |
| Phase 4 | PENDING | - | TBD | Institutional fixes |
| Phase 5 | PENDING | - | TBD | Grandmaster hardening |
| Phase 6 | PENDING | - | TBD | Optimization |

---

## Implementation Order

1. **Phase 1**: Fix ReplayEngine.cs (unblocks build)
2. **Phase 2**: CA Warning fixes (clean build)
3. **Phase 3**: Engineering hardening
4. **Phase 4**: Institutional fixes
5. **Phase 5**: Grandmaster hardening
6. **Phase 6**: Performance optimizations

