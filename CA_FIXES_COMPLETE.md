# HFT Platform - Complete CA Warning Fixes Implementation

## Files to Modify

### Core Module
1. **core/hft.core/IMetricsProvider.cs**
   - [ ] CA1051: Make MetricValue fields private (add properties)

2. **core/hft.core/PositionSnapshot.cs**
   - [ ] CA1051: Make public fields private (NetPosition, AvgEntryPrice, RealizedPnL, UnrealizedPnL, InstrumentId, AssetClass, Factors)

3. **core/hft.core/IEventLogger.cs**
   - [ ] CA1028: Change AuditRecordType underlying type from byte to int

4. **core/hft.core/Primitives.cs**
   - [ ] CA1028: Change OrderSide underlying type from byte to int
   - [ ] CA1062: Add null validation for bids/asks parameters in MarketDataTick constructor

5. **core/hft.core/ML/ModelRegistryAdapter.cs**
   - [ ] CA1062: Add null validation for model parameter in DeployModel

### Infrastructure Module
6. **infra/hft.infra/LatencyMonitor.cs**
   - [ ] CA1051: Make LatencyStats fields private (add properties)

7. **infra/hft.infra/AppendOnlyLog.cs**
   - [ ] CS9191: Change 'ref' to 'in' for struct parameter in Append method

8. **infra/hft.infra/CrossCloudSyncProvider.cs**
   - [ ] CA2007: Add ConfigureAwait(false)
   - [ ] CA1062: Add null validation for payload parameter

9. **infra/hft.infra/MetricsServer.cs**
   - [ ] CS0219: Remove unused variable 'newline'
   - [ ] CA2007: Add ConfigureAwait(false) for async operations

10. **infra/hft.infra/TickReplay.cs**
    - [ ] CA1303: Use resource strings for literal strings (suppress with justification)
    - [ ] CA1822: Mark ProcessRecord and CompareHashes as static

### Strategies Module
11. **strategies/hft.strategies/FeatureCrossValidator.cs**
    - [ ] CA1034: Move ValidationResult struct to file level
    - [ ] CA1051: Make public fields in FeatureCrossValidator private
    - [ ] CA1822: Mark ComputeSharpe as static

12. **strategies/hft.strategies/AlphaDecayMonitor.cs**
    - [ ] CA1852: Seal SignalHistory class

### Risk Module
13. **risk/hft.risk/ScenarioEngine.cs**
    - [ ] CA1051: Make ShockScenario fields private
    - [ ] CA1062: Add null validation for positions parameter

14. **risk/hft.risk/PortfolioRiskEngine.cs**
    - [ ] CA1062: Add null validation for snap parameter
    - [ ] CA1822: Mark PortfolioExposure as static (already a property, verify implementation)

## Build Verification
- [ ] Run `dotnet clean`
- [ ] Run `dotnet build`
- [ ] Verify ZERO warnings (45 warnings should become 0)

