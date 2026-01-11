# HFT Platform (C#/.NET 8.0)

Institutional-grade high-frequency trading platform monorepo.

## Architecture

- **Core**: Domain primitives, LockFreeRingBuffer, PnL Engine.
- **Feeds**: UDP Market Data Simulator & Listener.
- **Strategies**: Echo, MarketMaker.
- **Risk**: Pre-Trade Risk Engine (latency < 1us checks).
- **Execution**: ExecutionStub.
- **Backtest**: TickReplay & MatchingEngine.

## Build & Run

### Prerequisites

- .NET 8.0 SDK

### Windows (PowerShell)

```powershell
# Build
dotnet build -c Release

# Run Tests
dotnet test

# Run Simulation (Runner)
dotnet run --project Hft.Runner/Hft.Runner/Hft.Runner.csproj -c Release
```

### Linux

```bash
dotnet build -c Release
dotnet run --project Hft.Runner/Hft.Runner/Hft.Runner.csproj -c Release
```

## Monitoring

Metrics available at `http://localhost:9100/metrics` (Prometheus format) when Runner is active.

## Development

See `docs/` for runbooks and architectural deep dives.
