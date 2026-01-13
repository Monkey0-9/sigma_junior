using System;

namespace Hft.Execution.Models;

/// <summary>
/// Interface for latency simulation.
/// Models network, exchange, and processing delays.
/// GRANDMASTER: Essential for realistic HFT simulation.
/// </summary>
public interface ILatencyModel
{
    /// <summary>
    /// Generates a latency value (one-way) for an order.
    /// </summary>
    /// <returns>Latency in microseconds</returns>
    double GenerateLatency();
    
    /// <summary>
    /// Generates a latency value for an acknowledgment/market data update.
    /// </summary>
    /// <returns>Latency in microseconds</returns>
    double GenerateAckLatency();
}
