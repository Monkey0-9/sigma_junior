using System;
using System.Collections.Generic;
using Hft.Core;

namespace Hft.Execution.Models;

/// <summary>
/// Execution report from matching engine.
/// </summary>
public record ExecutionReport(
    bool IsFilled,
    double FilledQuantity,
    double LimitPrice,
    double Price,
    double Fee,
    long Timestamp
);

/// <summary>
/// Interface for a deterministic matching engine.
/// Simulates exchange matching logic including price-time priority.
/// GRANDMASTER: Core component for ERE v2.
/// </summary>
public interface IMatchingEngine
{
    /// <summary>
    /// Processes a new order and returns execution reports (fills as they happen).
    /// </summary>
    IEnumerable<ExecutionReport> ProcessOrder(OrderQueueEntry order, MarketLiquidity liquidity);
}
