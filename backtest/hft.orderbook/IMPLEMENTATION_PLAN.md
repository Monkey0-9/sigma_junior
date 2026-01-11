# Order-Book Simulator Implementation Plan
## Tier-1 Institutional Grade (BlackRock/Vanguard/Jane Street Level)

---

## Information Gathered

### Existing Architecture Analysis
- **Core Primitives**: Order (128-byte cache line aligned), Fill, MarketDataTick, PositionSnapshot - all blittable structs
- **Ring Buffer**: LockFreeRingBuffer with cache line padding for SPSC communication
- **Existing Backtest**: SimpleMatchingEngine (immediate fill only), TickReplay (CSV-based)
- **Strategy**: MarketMakerStrategy using ring buffers for order submission
- **Performance Focus**: Ultra-low-latency design with aggressive inlining, no allocations

### Design Requirements (Tier-1 Institutional)
1. **Determinism**: Same inputs → same outputs (critical for backtesting)
2. **Auditability**: Append-only event log for replay/debugging
3. **Performance**: Millions of ticks/hour replay capability
4. **Extensibility**: Multi-venue support with venue-specific rules
5. **Queue Position**: Accurate modeling of passive order fill timing
6. **Slippage**: Deterministic function based on book depth

---

## Plan: Exchange Order-Book Simulator (L2/L3)

### Phase 1: Data Models & Binary Log Format
**Files to create:**
1. `backtest/hft.orderbook/OrderBookTypes.cs` - L2/L3 book entries, order types, audit events
2. `backtest/hft.orderbook/BinaryLogWriter.cs` - Append-only binary log writer
3. `backtest/hft.orderbook/BinaryLogReader.cs` - Deterministic replay reader
4. `backtest/hft.orderbook/hft.orderbook.csproj` - Project file

### Phase 2: Matching Engine Core
**Files to create:**
1. `backtest/hft.orderbook/OrderBook.cs` - Price-time priority book with queue tracking
2. `backtest/hft.orderbook/MatchingEngine.cs` - Deterministic match with partial fills
3. `backtest/hft.orderbook/VenueRules.cs` - Venue-specific matching rules (hidden orders, mid-point)

### Phase 3: Queue Position & Slippage Modeling
**Files to create:**
1. `backtest/hft.orderbook/QueuePositionModel.cs` - Queue depth, time-to-fill estimation
2. `backtest/hft.orderbook/SlippageModel.cs` - Deterministic slippage vs depth function
3. `backtest/hft.orderbook/LatencyInjector.cs` - Per-venue latency distribution

### Phase 4: API & Interfaces
**Files to create:**
1. `backtest/hft.orderbook/IOrderBookSimulator.cs` - Main simulator interface
2. `backtest/hft.orderbook/SimulatorSession.cs` - Replay session with state management
3. `backtest/hft.orderbook/IOrderBookListener.cs` - Callback interface for fills/events

### Phase 5: Tests & Validation
**Files to create:**
1. `backtest/hft.orderbook.Tests/OrderBookTests.cs` - Unit tests for matching scenarios
2. `backtest/hft.orderbook.Tests/SimulatorIntegrationTests.cs` - Tick-level replay tests
3. `backtest/hft.orderbook.Tests/QueuePositionTests.cs` - Queue modeling validation

### Phase 6: Examples & Utilities
**Files to create:**
1. `backtest/hft.orderbook.Example/Program.cs` - Minimal runnable example
2. `backtest/hft.orderbook/Tools/CsvToBinaryLogConverter.cs` - CSV to binary log converter
3. `backtest/hft.orderbook/Tools/LogInspector.cs` - Binary log inspection tool

### Phase 7: Performance Optimization & Checkpointing
**Files to create:**
1. `backtest/hft.orderbook/OrderBookSnapshot.cs` - Book state serialization for checkpoints
2. `backtest/hft.orderbook/MemoryLayout.md` - Performance documentation

---

## Implementation Details

### 1. Data Model (Phase 1)

#### Binary Log Format
```
[Header: Magic(4) + Version(2) + VenueId(2) + InstrumentId(4) + StartTime(8)]
[Event 1: EventType(1) + Timestamp(8) + Payload(var)]
[Event 2: ...]
[Footer: EventCount(8) + Checksum(8)]
```

#### Event Types
- `0x01` - Order Add (L2 visible)
- `0x02` - Order Add (L3 hidden)
- `0x03` - Order Cancel
- `0x04` - Order Amend
- `0x05` - Trade (fill)
- `0x06` - Market Data Snapshot
- `0x07` - Heartbeat

### 2. Matching Engine (Phase 2)

#### Price-Time Priority Algorithm
```
1. Match incoming aggressive order against opposite side
2. For each price level:
   a. Process orders in FIFO arrival order (queue)
   b. Track queue position for passive orders
   c. Handle partial fills (update order, re-queue remainder)
3. If order not fully filled, add to book with timestamp

Tie-breaker: Order ID (unique, deterministic)
```

#### Queue Position Tracking
- Each order gets: `QueuePosition = TotalAhead + 1`
- On cancel/amend: Orders behind shift forward
- On fill: Orders behind shift forward

### 3. Slippage Model (Phase 3)

```
Slippage(aggressiveOrder) = 
    Sum over matched price levels:
        |FilledQtyAtLevel * (ExecPrice - ReferencePrice)| / OrderSize

Deterministic function based on:
- Depth at each price level (visible + hidden)
- Order size vs available liquidity
- Market impact coefficient (per venue)
```

### 4. Latency Injection (Phase 3)

```
Per-venue latency distribution:
- NASDAQ: Log-normal(μ=50μs, σ=20μs)
- NYSE: Log-normal(μ=80μs, σ=30μs)
- CME: Log-normal(μ=200μs, σ=50μs)

Inject before matching: SimulatedDelay(Random(venue.Distribution))
```

---

## Implementation Order (Step-by-Step)

### Step 1: Create project structure
- [ ] Create `backtest/hft.orderbook/hft.orderbook.csproj`
- [ ] Create `backtest/hft.orderbook/OrderBookTypes.cs`
- [ ] Create `backtest/hft.orderbook/BinaryLogFormat.cs`

### Step 2: Core data structures
- [ ] Implement `OrderBookEntry` (L2/L3)
- [ ] Implement `OrderEvent` (add/cancel/amend/trade)
- [ ] Implement `AuditLogEntry` with deterministic ordering

### Step 3: Matching engine
- [ ] Implement price-level queue with OrderEntry linked list
- [ ] Implement MatchingEngine with price-time priority
- [ ] Handle partial fills, hidden orders, mid-point logic

### Step 4: Queue position & slippage
- [ ] Implement QueuePositionModel
- [ ] Implement SlippageModel
- [ ] Implement LatencyInjector

### Step 5: API & interfaces
- [ ] Define `IOrderBookSimulator` interface
- [ ] Implement `OrderBookSimulator` class
- [ ] Implement `SimulatorSession` for replay

### Step 6: Binary log
- [ ] Implement `BinaryLogWriter` (append-only)
- [ ] Implement `BinaryLogReader` (deterministic replay)
- [ ] Implement snapshot/checkpoint support

### Step 7: Tests
- [ ] Write unit tests for matching scenarios
- [ ] Write integration tests for tick replay
- [ ] Validate against exchange data

### Step 8: Examples
- [ ] Create minimal runnable example
- [ ] Create CSV to binary converter
- [ ] Document performance considerations

---

## Dependencies
- `hft.core` - Core primitives (Order, Fill, MarketDataTick)
- `System.Memory` - Span<T>, Memory<T> optimizations
- `System.Collections.Immutable` - Immutable collections for snapshots

---

## Performance Targets
- **Throughput**: 10M+ ticks/hour per core
- **Latency**: < 1μs per tick processing (in-memory)
- **Memory**: < 100 bytes per order in book
- **Determinism**: Identical results across runs (seeded random)

---

## Risk Mitigation
1. **Determinism**: Use seeded random for all non-deterministic sources
2. **Auditability**: Every event logged with full context
3. **Testing**: Property-based testing for matching invariants
4. **Documentation**: Design rationale for each major decision

