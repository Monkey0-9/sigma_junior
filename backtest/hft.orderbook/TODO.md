# Order-Book Simulator Implementation - TODO

## Phase 1: Project Setup & Data Models

### Step 1: Create Project Structure
- [ ] Create `backtest/hft.orderbook/hft.orderbook.csproj`
- [ ] Add reference to `hft.core`
- [ ] Configure for .NET 8/10 with optimizations

### Step 2: Core Data Types
- [ ] Create `OrderBookTypes.cs`:
  - [ ] `OrderBookEntry` - L2 book level
  - [ ] `OrderQueueEntry` - Individual order in queue (L3)
  - [ ] `OrderType` enum (Limit, Market, Hidden, Iceberg)
  - [ ] `TimeInForce` enum (Day, IOC, FOK, GTC)
  - [ ] `OrderFlags` bitfield (Hidden, PostOnly, ReduceOnly)

### Step 3: Audit Event Types
- [ ] Create `AuditEvents.cs`:
  - [ ] `AuditEventType` enum
  - [ ] `AuditEvent` base struct
  - [ ] `OrderAddEvent`
  - [ ] `OrderCancelEvent`
  - [ ] `OrderAmendEvent`
  - [ ] `TradeEvent`
  - [ ] `SnapshotEvent`

### Step 4: Binary Log Format
- [ ] Create `BinaryLogFormat.cs`:
  - [ ] `LogHeader` struct
  - [ ] `LogFooter` struct
  - [ ] `EventSerializer` class for binary encoding
  - [ ] Define magic number and version

---

## Phase 2: Matching Engine Core

### Step 5: Price Level & Queue Data Structures
- [ ] Create `PriceLevel.cs`:
  - [ ] Doubly-linked list of orders
  - [ ] Aggregate size tracking
  - [ ] Visible vs hidden size
  - [ ] Insert order at correct queue position

### Step 6: Order Book
- [ ] Create `OrderBook.cs`:
  - [ ] Sorted dictionary of price levels (buy/sell sides)
  - [ ] Order lookup by OrderId (Dictionary)
  - [ ] Best bid/ask queries
  - [ ] Depth queries
  - [ ] Snapshot/restore methods

### Step 7: Matching Engine
- [ ] Create `MatchingEngine.cs`:
  - [ ] `Match(Order)` method
  - [ ] Price-time priority matching
  - [ ] Partial fill handling
  - [ ] Hidden order interaction
  - [ ] Post-only order handling
  - [ ] Generate trade events
  - [ ] Update order queue position

### Step 8: Venue Rules
- [ ] Create `VenueRules.cs`:
  - [ ] `IVenueRules` interface
  - [ ] `DefaultVenueRules` implementation
  - [ ] `NasdaqRules`, `NyseRules`, `CmeRules` (per-venue config)

---

## Phase 3: Queue Position & Slippage

### Step 9: Queue Position Model
- [ ] Create `QueuePositionModel.cs`:
  - [ ] `GetQueuePosition(orderId)` - position from front
  - [ ] `EstimateTimeToFill(orderId, marketData)` - time-based estimate
  - [ ] `GetAheadQuantity(orderId)` - quantity ahead in queue
  - [ ] Update queue on fills/cancels/amends

### Step 10: Slippage Model
- [ ] `SlippageModel.cs`:
  - [ ] `CalculateSlippage(order, bookState)` - deterministic function
  - [ ] `CalculateMarketImpact(order, bookState)` - temporary impact
  - [ ] Per-venue impact coefficients
  - [ ] Volume-weighted average price (VWAP) reference

### Step 11: Latency Injection
- [ ] `LatencyInjector.cs`:
  - [ ] `LatencyDistribution` interface
  - [ ] `LogNormalDistribution` implementation
  - [ ] `ApplyLatency(order, venue)` - inject simulated delay
  - [ ] Seeded random for determinism

---

## Phase 4: API & Interfaces

### Step 12: Simulator Interface
- [ ] `IOrderBookSimulator.cs`:
  - [ ] `InjectMarketData(tick)`
  - [ ] `SubmitOrder(order)`
  - [ ] `CancelOrder(orderId)`
  - [ ] `AmendOrder(orderId, newQty)`
  - [ ] `GetOrderBookState()`
  - [ ] `GetQueuePosition(orderId)`
  - [ ] `GetSlippageEstimate(order)`

### Step 13: Event Listeners
- [ ] `IOrderBookListener.cs`:
  - [ ] `OnTrade(trade)`
  - [ ] `OnOrderAdded(order)`
  - [ ] `OnOrderCanceled(order)`
  - [ ] `OnOrderAmended(order)`
  - [ ] `OnBestBidAskChanged(bid, ask)`

### Step 14: Replay Session
- [ ] `SimulatorSession.cs`:
  - [ ] `StartReplay(logFile)`
  - [ ] `Step()` - process single event
  - [ ] `Run()` - run to completion
  - [ ] `Checkpoint()`
  - [ ] `Restore(checkpoint)`

---

## Phase 5: Binary Log System

### Step 15: Log Writer
- [ ] `BinaryLogWriter.cs`:
  - [ ] Append-only file writing
  - [ ] Event serialization
  - [ ] Header/Footer writing
  - [ ] Flush on close

### Step 16: Log Reader
- [ ] `BinaryLogReader.cs`:
  - [ ] Sequential event reading
  - [ ] Random access by event index
  - [ ] Deterministic replay (no buffering)
  - [ ] Event callbacks

### Step 17: Snapshot System
- [ ] `OrderBookSnapshot.cs`:
  - [ ] Serialize book state
  - [ ] Serialize orders
  - [ ] Serialize pending events
  - [ ] Compressed snapshots (optional)

---

## Phase 6: Tests & Validation

### Step 18: Unit Tests
- [ ] `OrderBookTests.cs`:
  - [ ] Simple price-time match
  - [ ] Partial fill scenario
  - [ ] Multiple orders at same price
  - [ ] Cancel behavior
  - [ ] Amend behavior
  - [ ] Hidden order matching
  - [ ] Post-only orders

### Step 19: Queue Position Tests
- [ ] `QueuePositionTests.cs`:
  - [ ] Queue position calculation
  - [ ] Queue updates on fills
  - [ ] Queue updates on cancels
  - [ ] Time-to-fill estimation

### Step 20: Integration Tests
- [ ] `SimulatorIntegrationTests.cs`:
  - [ ] Replay from binary log
  - [ ] Match against reference data
  - [ ] Determinism verification (run twice, compare)

### Step 21: Performance Tests
- [ ] `PerformanceTests.cs`:
  - [ ] Throughput benchmark (ticks/second)
  - [ ] Memory usage benchmark
  - [ ] Latency histogram

---

## Phase 7: Examples & Utilities

### Step 22: Minimal Example
- [ ] `examples/BasicOrderBook.cs`:
  - [ ] Create simulator
  - [ ] Initialize with snapshot
  - [ ] Submit orders
  - [ ] Print fills and queue positions

### Step 23: CSV Converter
- [ ] `tools/CsvToBinaryLog.cs`:
  - [ ] Parse exchange L2 CSV
  - [ ] Convert to binary log format
  - [ ] Validate output

### Step 24: Log Inspector
- [ ] `tools/LogInspector.cs`:
  - [ ] Print log summary
  - [ ] Export to human-readable format
  - [ ] Verify log integrity

---

## Phase 8: Documentation & Optimization

### Step 25: Performance Documentation
- [ ] `docs/MemoryLayout.md`:
  - [ ] Cache line alignment details
  - [ ] Memory pool usage
  - [ ] Index structures

### Step 26: API Documentation
- [ ] `docs/ApiUsage.md`:
  - [ ] Quick start guide
  - [ ] Advanced usage patterns
  - [ ] Best practices

### Step 27: Design Documentation
- [ ] `docs/Design.md`:
  - [ ] Architecture overview
  - [ ] Design decisions & rationale
  - [ ] Trade-offs

---

## Progress Tracking

| Phase | Status | Completed Steps |
|-------|--------|-----------------|
| Phase 1: Data Models | ⏳ Pending | 0/4 |
| Phase 2: Matching Engine | ⏳ Pending | 0/4 |
| Phase 3: Queue & Slippage | ⏳ Pending | 0/3 |
| Phase 4: API & Interfaces | ⏳ Pending | 0/3 |
| Phase 5: Binary Log | ⏳ Pending | 0/3 |
| Phase 6: Tests | ⏳ Pending | 0/4 |
| Phase 7: Examples | ⏳ Pending | 0/3 |
| Phase 8: Documentation | ⏳ Pending | 0/3 |

---

## Notes

### Key Design Principles
1. **Determinism First**: Every code path must be deterministic
2. **Zero Allocations**: Hot paths should not allocate
3. **Cache Line Aligned**: 64-byte alignment for hot data
4. **Auditability**: Every event logged with full context

### Performance Targets
- **Throughput**: 10M+ ticks/hour per core
- **Latency**: < 1μs per tick processing
- **Memory**: < 100 bytes per order in book

### Testing Strategy
1. Unit tests for each component
2. Property-based testing for matching invariants
3. Determinism verification (run 1000x, compare)
4. Integration tests with real market data

