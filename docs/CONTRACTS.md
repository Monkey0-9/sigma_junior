# Binary & Architectural Contracts

## 1. Zero-Allocation Domain Types
All structs below are `Pack=1` sequential layout.

### MarketDataTick (L2 Snapshot)
| Field | Type | Offset | Description |
|---|---|---|---|
| Version | byte | 0 | Schema Version (currently 1) |
| Sequence | long | 1 | Monotonic sequence number |
| InstrumentId | long | 9 | Unique asset identifier |
| SendTicks | long | 17 | Venue egress timestamp |
| ReceiveTicks | long | 25 | Node ingress timestamp |
| Bid1-5 | PriceLevel | 33-112 | 5-deep Bid Depth (Price, Size) |
| Ask1-5 | PriceLevel | 113-192 | 5-deep Ask Depth (Price, Size) |

### Order Primitive
| Field | Type | Offset | Description |
|---|---|---|---|
| Version | byte | 0 | Schema Version |
| OrderId | long | 1 | Unique Node Order ID |
| InstrumentId | long | 9 | asset identifier |
| Side | byte | 17 | 1=Buy, 2=Sell |
| Price | double | 18 | IEEE 754 |
| Quantity | double | 26 | IEEE 754 |

## 2. Governance Signature
Audit frames are signed with HMAC-SHA256:
`Signature = HMAC_SHA256(Header + Payload, Key)`
- Key rotation policy: Every 24h or per trading session.
- Failure Mode: Any signature mismatch during replay MUST halt the backtest process (Fail-Fast).
