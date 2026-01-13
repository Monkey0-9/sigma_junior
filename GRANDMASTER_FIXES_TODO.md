# INSTITUTIONAL GRANDMASTER FIXES - TODO LIST

## Phase 1: Project Configuration (.csproj files)
- [x] 1.1 Fix core/hft.core/hft.core.csproj
- [x] 1.2 Fix feeds/hft.feeds/hft.feeds.csproj
- [x] 1.3 Fix strategies/hft.strategies/Hft.Strategies.csproj
- [x] 1.4 Fix infra/hft.infra/hft.infra.csproj
- [x] 1.5 Fix risk/hft.risk/hft.risk.csproj
- [x] 1.6 Fix execution/hft.execution/hft.execution.csproj
- [x] 1.7 Fix Hft.Runner/Hft.Runner.csproj
- [x] 1.8 Fix backtest/hft.backtest/hft.backtest.csproj

## Phase 2: Async ConfigureAwait Fixes
- [x] 2.1 UdpMarketDataListener.cs - Already has ConfigureAwait(false)
- [x] 2.2 ExecutionEngine.cs - Already has ConfigureAwait(false)

## Phase 3: Dispose Pattern Fixes
- [x] 3.1 Fix PreTradeRiskEngine.cs Dispose(bool) access modifier (private → protected virtual)
- [x] 3.2 Seal UdpMarketDataSimulator.cs
- [x] 3.3 Dispose _key field in AppendOnlyLog.cs
- [x] 3.4 StructuredEventLogger.cs - Already has protected virtual

## Phase 4: Seal Classes
- [x] 4.1 Seal MetricsCounter.cs
- [x] 4.2 Seal RiskLimits.cs
- [x] 4.3 Seal SymbolLimit
- [x] 4.4 Seal LatencyMonitor.cs

## Phase 5: Exception Handling
- [x] 5.1 UdpMarketDataListener.cs CA1031 - Already fixed with specific exception types

## Phase 6: Validation
- [x] 6.1 Run dotnet clean
- [x] 6.2 Run dotnet build
- [x] 6.3 Verify zero warnings and errors

## ALL GRANDMASTER FIXES COMPLETE ✅


