# CA Warning Fixes - Implementation Progress

## Phase 1: Security Fixes
- [ ] `UdpMarketDataSimulator.cs:68` - Replace insecure Random with RandomNumberGenerator (CA5394)
- [ ] `PreTradeRiskEngine.cs:9` - Implement IDisposable for _cts (CA1001)

## Phase 2: Method to Property Conversions (CA1024)
- [ ] `MultiAssetPnlEngine.GetTotalPortfolioValueUsd()` → `TotalPortfolioValueUsd` property
- [ ] `PortfolioRiskEngine.GetPortfolioExposure()` → `PortfolioExposure` property

## Phase 3: Exception Handling (CA1031) - Catch Specific Exceptions
- [ ] `UdpMarketDataListener.Stop()` - Catch specific exceptions
- [ ] `UdpMarketDataListener.RunLoop()` - Catch specific exceptions
- [ ] `UdpMarketDataSimulator.Stop()` - Catch specific exceptions
- [ ] `MetricsServer.Start()` - Catch specific exceptions
- [ ] `MetricsServer.Stop()` - Catch specific exceptions
- [ ] `MetricsServer.RunLoop()` - Catch specific exceptions
- [ ] `CrossCloudSyncProvider.SyncStateAsync()` - Catch specific exceptions

## Phase 4: Parameter Modifiers (CS9191)
- [ ] `AppendOnlyLog.Append()` - Use 'in' instead of 'ref' for struct parameter

## Phase 5: Remove Default Initialization (CA1805)
- [ ] `FilesystemBootstrap._initialized` - Remove explicit default initialization

## Phase 6: Nested Types to File Level (CA1034)
- [ ] `RegulatoryComplianceDashboard.ComplianceMetric` - Move to file level
- [ ] `FeatureCrossValidator.ValidationResult` - Move to file level
- [ ] `ScenarioEngine.ShockScenario` - Move to file level

## Phase 7: Collection Type Changes (CA1002)
- [ ] `MetricsServer._counters` - Change List<MetricsCounter> to IReadOnlyList<MetricsCounter>

## Phase 8: Localization (CA1303, CA1305)
- [ ] `MetricsServer.Start()` - Use resource strings for literals
- [ ] `TickReplay.Replay()` - Use resource strings for literals
- [ ] `MetricsServer.RunLoop()` - Add IFormatProvider to StringBuilder.AppendLine calls

## Phase 9: Stream WriteAsync (CA1835)
- [ ] `MetricsServer.RunLoop()` - Use Stream.WriteAsync(ReadOnlyMemory<byte>, CancellationToken)

## Phase 10: Naming Conventions (CA1707, CA1052)
- [ ] `Hft.Benchmarks.Program.cs` - Rename `TryWrite_TryRead` to `TryWriteTryRead`
- [ ] `Hft.Benchmarks.Program.cs` - Make Program class static or sealed

## Phase 11: Seal Internal Classes (CA1852)
- [ ] `AlphaDecayMonitor.SignalHistory` - Seal the class

## Phase 12: Build & Verify
- [ ] Run `dotnet build` to verify all warnings are fixed

