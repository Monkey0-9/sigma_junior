# Operations Runbook

## Logging

- Uses **Binary Append-Only** format in production `data/logs/transaction_<date>.bin`.
- **Replay**: `Hft.ReplayTool` reads binary logs and reconstructs state (uses `PnlEngine`).

## Start of Day

1. Archive yesterday's logs.
2. Update `RiskLimits` config file.
3. Start `Hft.Runner` with `--mode live`.
4. Verify Prometheus metrics are non-zero.

## Kill Switch

- **Soft**: Set `RiskLimits.KillSwitchActive = true` via config file watcher or HTTP admin port.
- **Hard**: `kill -9 <pid>`. `OrderEntry` gateway will cancel on disconnect (Exchange side).

## Restore

1. Start Runner with `--restore <last_snapshot_id>`.
2. Replay fills from drop-copy since snapshot.
