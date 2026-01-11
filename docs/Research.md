# Research & Exchange Simulator

## Levels of Simulation

1. **L1 (Current)**: Feed of BBO. Immediate fill if crosses.
2. **L2 (Book)**: Maintain full OrderBook state. Queue position estimation.
   - Match only when "my" queue position is front.
3. **L3 (Market Impact)**: Simulate adverse selection. If I buy huge qty, move price against me.

## Specs

- `ExchangeSimulator` must implement `IExecutionVenue`.
- Latency model: `Wait(Random(10us, 50us))`.
