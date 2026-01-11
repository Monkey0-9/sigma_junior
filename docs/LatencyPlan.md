# Latency Optimization Plan

## Host Tuning

1. **CPU Pinning**: Isolate cores `2-7` for HFT. Move OS interrupts to core `0-1`.
   - `isolcpus=2-7` kernel boot arg.
   - Use `Process.GetCurrentProcess().ProcessorAffinity` in `Hft.Runner`.
2. **Network**:
   - `ethtool -C eth0 adaptive-rx off` (disable interrupt coalescing).
   - Use Solarflare OpenOnload or DPDK for kernel bypass.

## Application Tuning

1. **GC**: `GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency`.
2. **Allocations**: Remove all `new` in `RunLoop`. Use `ArrayPool` or pre-allocated RingBuffers (DONE).
3. **Data Structures**: Align `MarketDataTick` to 64-byte cache line (padding).

## Microbenchmarks

- Use `BenchmarkDotNet` for `RingBuffer.TryWrite`.
- Use Histogram for "Time from IO Read to Order Write".
