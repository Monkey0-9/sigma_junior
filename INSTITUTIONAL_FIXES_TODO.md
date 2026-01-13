# Institutional Zero-Warning Build Fixes - Implementation Plan

## STEP 1: Infrastructure & Configuration
- [ ] Remove global NoWarn suppressions from Directory.Build.props
- [ ] Add strategies project reference to Hft.Runner.csproj

## STEP 2: Risk Module (hft.risk)
- [ ] ScenarioEngine.cs - Move ShockScenario struct to file level (CA1034)
- [ ] PreTradeRiskEngine.cs - Catch specific exceptions (CA1031)

## STEP 3: Execution Module (hft.execution)
- [ ] ExecutionEngine.cs - Fix CA1001 (IDisposable), CA1031, CA5394 (Random)

## STEP 4: Backtest Module
- [ ] SimpleMatchingEngine.cs - Fix CA1001 (IDisposable), CA1805, CA1031
- [ ] BacktestNode.cs - Fix CA1063/CA1816 (Dispose pattern), CA2213 (dispose fields)

## STEP 5: FIOS Module
- [ ] Hft.FIOS.cs - Fix CA1815 (DecisionRequest, SystemState), CA5394, CA1823

## STEP 6: Benchmarks
- [ ] Hft.Benchmarks/Program.cs - Fix CA1052 (static class), CA1707 (underscores)

## STEP 7: Runner Module
- [ ] Program.cs - Fix CA1307, CA1305, CA1303, CA1031, CA1852
- [ ] TradingEngine.cs - Fix CA1063/CA1816, CA1305, CA1837, CA1031, CA2213

## STEP 8: Validation
- [ ] dotnet clean
- [ ] dotnet build (verify ZERO warnings)
- [ ] dotnet run -c Release (verify no runtime errors)

