using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hft.Core;

namespace Hft.OrderBook
{
    // Types moved to Hft.Core.Primitives.cs
    // - OrderType
    // - TimeInForce
    // - OrderAttributes
    // - OrderStatus
    // - OrderBookEntry
    // - OrderQueueEntry
    // - BestBidAsk
    // - BookDepth

    /// <summary>
    /// Order event type for audit logging.
    /// Kept here as it feels local to OrderBook events, unless needed elsewhere.
    /// </summary>
    public enum OrderEventType
    {
        None = 0,
        Add = 1,
        Cancel = 2,
        Amend = 3,
        Fill = 4,
        Reject = 5,
        Expire = 6,
        BboChange = 7,
        Trade = 8
    }
}
