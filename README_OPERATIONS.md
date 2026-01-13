# HFT Platform Operations Guide

This guide details the operational procedures for running, monitoring, and managing the HFT Platform in production and simulation environments.

## Deployment & Startup

- **Build**: Run `dotnet build -c Release`. Ensure no warnings or errors.
- **Configuration**: Review `config.json`. Set `Mode` to `Live` or `Paper`.
- **Permissions**: Run the following to reserve the metrics URL:

```powershell
netsh http add urlacl url=http://127.0.0.1:9180/metrics/ user=%USERDOMAIN%\%USERNAME%
```

- **Launch**: Execute `Hft.Runner.exe`.

## Monitoring (Observability)

- **Prometheus**: Scrape `http://127.0.0.1:9180/metrics/`.
- **Key Metrics**:
  - `hft_executed_orders_total`: Cumulative count of filled orders.
  - `hft_pnl_realized`: Current realized profit/loss.
  - `hft_signal_throttle`: Current throttling factor for alpha signals (0 = disabled, 1 = full).
  - `hft_execution_latency_p99`: Tail latency for order execution.

## Risk Controls & Safety

- **Kill Switch**: Press `Ctrl+C` for graceful shutdown. The system will attempt to cancel all open orders before exiting.
- **Auto-Throttle**: The `SignalQualityManager` will automatically reduce trade sizes if the Information Coefficient (IC) drops below threshold.
- **Regulatory Logs**: Audit trail is stored in `logs/audit_*.log` in binary format. Use `TickReplay` tool to decode.

## Troubleshooting

- **High Latency**: Check for GC pauses or CPU throttling. Ensure process affinity is set if necessary.
- **Metrics Server Fails**: Check if port 9180 is in use or if you lack HTTP reservation permissions.
- **Build Failures**: Adhere strictly to institutional coding standards (CAXXXX).

## Architecture Layers (FIOS)

- **L1 - Core Primitives**: Types, RingBuffer.
- **L2 - Connectivity**: Market Data Feeds, Venue Adapters.
- **L3 - Infrastructure**: Metrics, Logging, Persistence.
- **L4 - Risk**: Pre-trade limits, Stress testing.
- **L5 - Execution**: SOR, Almgren-Chriss, Matching.
- **L6 - Strategies**: Alpha logic, Signal monitors.
