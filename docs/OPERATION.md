# HFT Platform: Operational Runbook (Aladdin-Class)

## 1. Safety Procedures
- **Global Kill Switch**: If the system behaves erratically, the `PreTradeRiskEngine` will automatically stop order submission if the `KillSwitchActive` flag is set in the runtime configuration.
- **Panic Threshold**: If the daily loss exceeds the `DailyLossLimit`, the system enters a self-curbing state.

## 2. Process Management
- **Startup**: `dotnet run --project Hft.Runner --metrics-port 9180 --udp-port 5005`
- **Graceful Shutdown**: Send `SIGINT` (Ctrl+C). The engine will flush audit logs and release PID locks.
- **Shadow Copy**: Use `--shadow-copy-output <path>` to run the engine in a locked, immutable directory.

## 3. Governance & Audit
- **Audit Logs**: Located in `./data/audit/*.bin`.
- **Integrity Check**: Use the `TickReplay` tool to verify the HMAC signatures:
  `dotnet run --project Hft.Tools.Replay --path ./data/audit/audit_latest.bin --key <HMAC_KEY>`

## 4. Monitoring
- **Prometheus**: Metrics are available at `http://localhost:9180/metrics`.
- **Latency**: High-precision `p99` latency is logged every second to the console.

## 5. Failover
- **Cross-Cloud Sync**: The `CrossCloudSyncProvider` replicates state every 60s to the backup cloud endpoint defined in the environmental variables.
