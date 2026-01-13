# GRANDMASTER ARCHITECTURE SUMMARY

## Module Responsibilities

### Core (hft.core)
**Domain primitives and invariants - single source of truth**
- `Primitives.cs`: Order, Fill, MarketDataTick, PriceLevel, OrderSide
- `PositionSnapshot.cs`: Thread-safe position tracking with cache line padding
- `LockFreeRingBuffer.cs`: Single-producer/single-consumer ring buffer
- `PnlEngine.cs`: Realized/unrealized PnL computation
- `RiskLimits.cs`: Hierarchical risk configuration
- `EngineState.cs`: State machine for trading node lifecycle

### Infrastructure (hft.infra)
**I/O, observability, and system services**
- `AppendOnlyLog.cs`: HMAC-signed binary audit trail
- `CompositeEventLogger.cs`: Dual-path logging (binary + JSONL)
- `StructuredEventLogger.cs`: Human-readable JSONL logs
- `MetricsServer.cs`: Prometheus-compatible metrics endpoint
- `LatencyMonitor.cs`: Tick-to-order latency tracking
- `FilesystemBootstrap.cs`: Deterministic directory creation

### Feeds (hft.feeds)
**Market data ingestion**
- `UdpMarketDataListener.cs`: UDP listener writing to ring buffer
- `UdpMarketDataSimulator.cs`: Test data generator

### Risk (hft.risk)
**Pre-trade and post-trade risk management**
- `PreTradeRiskEngine.cs`: Order validation and rate limiting
- (PortfolioRiskEngine.cs pending)

### Execution (hft.execution)
**Order execution and fill processing**
- `ExecutionEngine.cs`: Simulated venue with latency modeling

### Strategies (hft.strategies)
**Trading strategies**
- `MarketMakerStrategy.cs`: L2 imbalance-based market making

### Runner (Hft.Runner)
**Application entry point and orchestrator**
- `Program.cs`: CLI argument parsing and shutdown handling
- `TradingEngine.cs`: Component lifecycle management

---

## Non-Negotiable Rules (GRANDMASTER STANDARD)

### 1. Domain Ownership
- All domain primitives MUST be defined in **Core** only
- No duplicate types in Feeds/Strategies/Execution/Risk
- Use `using Hft.Core;` exclusively for domain types

### 2. Immutability
- Order, Fill, MarketDataTick MUST be immutable structs
- Use functional `WithX()` methods for derived state
- Never mutate domain objects after creation

### 3. Lifecycle Safety
- All long-running components MUST:
  - Accept `CancellationToken` in Start/Run methods
  - Expose `Start()`, `Stop()`, `Dispose()` pattern
  - Initialize `Task` fields as `Task.CompletedTask` or nullable
  - Implement proper `IDisposable` with `GC.SuppressFinalize`

### 4. File System
- Create directories on startup (fail fast on permission errors)
- Use explicit `FileShare` flags when opening files
- Prefer `FileMode.Append` for audit logs

### 5. Network Security
- Default to unprivileged ports (9180 for metrics)
- Handle `HttpListenerException` gracefully
- Provide guidance for privileged port binding

### 6. Build Quality
- **ZERO warnings** with `/p:TreatWarningsAsErrors=true`
- Suppress only justified analyzer rules with documentation
- CA1063: Implement proper dispose pattern
- CA1031: Catch specific exceptions only when justified

### 7. Observability
- All state transitions logged with UTC timestamps
- Monotonic sequence IDs in audit records
- Structured JSON logging for machine parsing

### 8. Testability
- Deterministic seeds for any randomness in tests
- Unit tests for domain primitive serialization
- Integration smoke tests for core workflows

---

## Directory Structure

```
hft_platform/
├── core/hft.core/           # Domain primitives (canonical)
├── infra/hft.infra/         # I/O, logging, metrics
├── feeds/hft.feeds/         # Market data sources
├── risk/hft.risk/           # Risk engines
├── execution/hft.execution/ # Order execution
├── strategies/hft.strategies/ # Trading strategies
├── Hft.Runner/              # Application host
├── backtest/                # Backtesting framework
├── tests/                   # Unit/integration tests
├── data/                    # Runtime data (audit, logs, replay)
├── docs/                    # Architecture documentation
└── tools/                   # DevOps scripts
```

---

## Critical Hot Paths

| Component | Latency Target | Mechanism |
|-----------|---------------|-----------|
| MarketDataTick → Strategy.OnTick | < 100ns | ref struct, stackalloc |
| Order → RingBuffer.Write | < 50ns | lock-free SPSC |
| Risk.CheckRisk | < 500ns | in parameters, no allocations |
| MetricsServer.WriteResponse | < 1ms | async I/O, StringBuilder pooling |

---

## Build Commands

```powershell
# Clean build with warnings as errors
dotnet clean; dotnet build -c Release -p:TreatWarningsAsErrors=true

# Run tests
dotnet test --no-build -c Release

# Production publish
dotnet publish -c Release -o ./publish
```

