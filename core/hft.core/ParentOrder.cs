using System;

namespace Hft.Core;

/// <summary>
/// Represents a parent order to be executed by the system.
/// GRANDMASTER: Domain primitive for order execution.
/// </summary>
public class ParentOrder
{
    public long ParentOrderId { get; set; }
    public long InstrumentId { get; set; }
    public OrderSide Side { get; set; }
    public double TotalQuantity { get; set; }
    public double? LimitPrice { get; set; }
    public OrderType Type { get; set; }
    public OrderIntent Intent { get; set; }
    public ExecutionStrategy Strategy { get; set; }
    public RoutingStrategyParameters StrategyParameters { get; set; } = new();
    public long Timestamp { get; set; }
    public string ClientId { get; set; } = string.Empty;
}

public enum OrderIntent
{
    Aggressive,
    Passive,
    Neutral
}

public enum ExecutionStrategy
{
    POV,
    TWAP,
    VWAP,
    AlmgrenChriss,
    ImplementationShortfall
}

public class RoutingStrategyParameters
{
    public double PovPercentage { get; set; } = 0.1;
    public int MinSliceSize { get; set; } = 100;
    public int MaxSliceSize { get; set; } = 10000;
    public int TwapIntervalCount { get; set; } = 20;
    public double VwapParticipationLimit { get; set; } = 0.15;
    
    // Almgren-Chriss params
    public double DailyVolatility { get; set; } = 0.02;
    public double RiskAversion { get; set; } = 1.0;
    public double TemporaryImpact { get; set; } = 0.001;
}
