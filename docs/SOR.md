# Smart Order Router (SOR) & Multi-Venue Plan

## Design

- **Interface**: `IExecutionVenue` (SendOrder, CancelOrder, GetOrderBook).
- **Venues**: MockVenue (latency simulation), Binance, CME.
- **Routing Logic**:
  - Maintain latency map per venue (EMA of `Ack - SendTime`).
  - `SmartOrderRouter` class subscribes to multiple `IExecutionVenue`.
  - Strategy sends order to SOR.
  - SOR picks venue with best Price + ProbabilityOfFill (calculated via queue position).

## Code Skeleton

```csharp
public interface IExecutionVenue {
    void Send(Order order);
    event Action<Fill> OnFill;
}
```
