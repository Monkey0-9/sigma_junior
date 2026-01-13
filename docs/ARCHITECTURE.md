# Aladdin Architecture: Technical Blueprint

## 1. Governance Layer (Deterministic Auditing)
All state-changing events (Order placement, Risk Rejection, Fill) are persisted to an **Append Only Binary Log** using HMAC-SHA256 signatures for tamper-evident playback.
- **Contract**: [CONTRACTS.md](CONTRACTS.md)
- **Engine**: [AppendOnlyLog.cs](file:///c:/hft_platform/infra/hft.infra/AppendOnlyLog.cs)

## 2. Immutable Domain Primitives
The `Hft.Core` project provides the single source of truth for all domain types.
- **Immutability**: All primitives (`Order`, `Fill`, `MarketDataTick`) are `readonly struct` to enforce zero-allocation, thread-safe passing.
- **Memory Layout**: `[StructLayout(LayoutKind.Sequential, Pack=1)]` ensures bit-identical serialization for bit-perfect replay.

## 3. State Ownership & Isolation
- **Authority**: the [PreTradeRiskEngine](file:///c:/hft_platform/risk/Hft.Risk/PreTradeRiskEngine.cs) is the sole authoritative writer of the `PositionSnapshot`.
- **Read-Only Gate**: Strategies and Execution layers interact via [IPositionSnapshotReader](file:///c:/hft_platform/core/hft.core/IPositionSnapshotReader.cs), preventing uncoordinated state mutation.

## 4. Operational Integrity
- **Lifecycle**: Task-based orchestration with linked `CancellationToken` for graceful, deterministic shutdown.
- **Process Protection**: PID file locking to prevent duplicate node instantiation.
- **Monitoring**: Prometheus metrics on non-privileged port 9180.
