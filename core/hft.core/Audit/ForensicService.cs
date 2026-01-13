using System;
using System.Collections.Generic;
using System.Linq;

namespace Hft.Core.Audit;

/// <summary>
/// Forensic API for regulatory and internal compliance queries.
/// Answers: "Why did order X get filled?" in < 1 second.
/// GRANDMASTER: The authoritative interface for trade justification and audit.
/// </summary>
public sealed class ForensicService
{
    private readonly ReplayEngine _replayEngine;
    private readonly IEventLogger _auditLog;

    public ForensicService(ReplayEngine replayEngine, IEventLogger auditLog)
    {
        _replayEngine = replayEngine ?? throw new ArgumentNullException(nameof(replayEngine));
        _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
    }

    /// <summary>
    /// Answer the fundamental question: "Why was this order filled?"
    /// </summary>
    public TradeJustification AnalyzeTrade(long orderId, DateTime tradeTime)
    {
        var startTime = DateTime.UtcNow;

        // Step 1: Retrieve the complete order history
        var orderHistory = _replayEngine.GetOrderHistory(orderId);
        if (orderHistory.Count == 0)
        {
            _auditLog.LogWarning("FORENSICS", $"No order history found for orderId={orderId}");
            return TradeJustification.NotFound(orderId);
        }

        // Step 2: Find the trade cause
        var cause = _replayEngine.FindTradeCause(orderId, tradeTime);
        if (cause is null)
        {
            _auditLog.LogWarning("FORENSICS", $"No trade cause found for orderId={orderId}");
            return TradeJustification.NotFound(orderId);
        }

        // Step 3: Analyze preceding market conditions
        var marketCondition = AnalyzeMarketCondition(cause.Value.PrecedingMarketData);

        // Step 4: Construct justification
        var justification = new TradeJustification(
            OrderId: orderId,
            OrderSubmitTime: cause.Value.SubmitTime,
            TradeTime: cause.Value.TradeTime,
            Reason: DetermineReason(cause.Value, marketCondition),
            MarketCondition: marketCondition,
            ExecutionPath: orderHistory.ToList().AsReadOnly(),
            AnalysisTimeMs: (DateTime.UtcNow - startTime).TotalMilliseconds,
            IsRegulatorySafe: VerifyRegulatoryCompliance(orderHistory, cause.Value)
        );

        _auditLog.LogInfo("FORENSICS", 
            $"Trade analysis complete for orderId={orderId}: {justification.Reason} (compliance={justification.IsRegulatorySafe})");

        return justification;
    }

    /// <summary>
    /// Retrieve all violations for a specific order.
    /// </summary>
    public RiskViolationSummary GetRiskViolations(long orderId)
    {
        var orderHistory = _replayEngine.GetOrderHistory(orderId);
        var violations = orderHistory
            .Where(e => e.Type == AuditRecordType.RiskViolation)
            .ToList();

        return new RiskViolationSummary(
            OrderId: orderId,
            ViolationCount: violations.Count,
            Violations: violations.AsReadOnly()
        );
    }

    /// <summary>
    /// Generate a complete audit trail for an order in human-readable format.
    /// </summary>
    public string GenerateHumanReadableAuditTrail(long orderId)
    {
        var orderHistory = _replayEngine.GetOrderHistory(orderId);
        var lines = new List<string>
        {
            $"=== Audit Trail for Order {orderId} ===",
            $"Total Events: {orderHistory.Count}",
            ""
        };

        foreach (var evt in orderHistory.OrderBy(e => e.Timestamp))
        {
            lines.Add($"[{evt.Timestamp:HH:mm:ss.ffffff}] {evt.Type}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Analyze market conditions preceding a trade decision.
    /// </summary>
    private static MarketConditionAnalysis AnalyzeMarketCondition(IReadOnlyList<AuditEventRecord> tickEvents)
    {
        if (tickEvents.Count == 0)
            return MarketConditionAnalysis.Stable();

        var recentTicks = tickEvents.OrderByDescending(e => e.Timestamp).Take(5).ToList();
        
        return new MarketConditionAnalysis(
            RecentTickCount: recentTicks.Count,
            LastTickTime: recentTicks.FirstOrDefault().Timestamp,
            PriceVolatility: EstimatePriceVolatility(recentTicks),
            VolumeRegime: EstimateVolumeRegime(recentTicks)
        );
    }

    /// <summary>
    /// Determine the primary reason for a trade occurring.
    /// </summary>
    private static string DetermineReason(ForensicTrace trace, MarketConditionAnalysis mkt)
    {
        var duration = trace.DurationMs;

        if (duration < 10)
            return "IMMEDIATE_EXECUTION: Market matched order within 10ms";
        
        if (duration < 100 && mkt.VolumeRegime > 0.7)
            return "HIGH_VOLUME: Matched during high-volume regime";
        
        if (mkt.PriceVolatility > 0.5)
            return "VOLATILE_MARKET: Filled during market volatility";
        
        return "NORMAL_EXECUTION: Standard order matching";
    }

    /// <summary>
    /// Verify that the trade complies with regulatory requirements.
    /// </summary>
    private static bool VerifyRegulatoryCompliance(IReadOnlyList<AuditEventRecord> orderHistory, ForensicTrace trace)
    {
        // Check for risk violations during order lifecycle
        var hasRiskViolation = orderHistory.Any(e => e.Type == AuditRecordType.RiskViolation);
        
        // Check that trade occurred shortly after submission (no suspicious delay)
        var submissionToTrade = (trace.TradeEvent.Timestamp - trace.SubmitEvent.Timestamp).TotalSeconds;
        var suspiciousDelay = submissionToTrade > 60; // More than 60 seconds

        return !hasRiskViolation && !suspiciousDelay;
    }

    /// <summary>
    /// Estimate price volatility from recent ticks.
    /// </summary>
    private static double EstimatePriceVolatility(List<AuditEventRecord> ticks)
    {
        // Placeholder: in real implementation, extract prices from tick payloads
        // and calculate standard deviation
        return 0.3;
    }

    /// <summary>
    /// Estimate volume regime from recent ticks.
    /// </summary>
    private static double EstimateVolumeRegime(List<AuditEventRecord> ticks)
    {
        // Placeholder: in real implementation, extract volumes from tick payloads
        // and estimate percentile rank
        return 0.5;
    }
}

/// <summary>
/// Comprehensive justification for why a trade was executed.
/// </summary>
public sealed record TradeJustification(
    long OrderId,
    DateTime OrderSubmitTime,
    DateTime TradeTime,
    string Reason,
    MarketConditionAnalysis MarketCondition,
    IReadOnlyList<AuditEventRecord> ExecutionPath,
    double AnalysisTimeMs,
    bool IsRegulatorySafe
)
{
    /// <summary>
    /// Create a "not found" justification when order doesn't exist.
    /// </summary>
    public static TradeJustification NotFound(long orderId)
    {
        return new TradeJustification(
            OrderId: orderId,
            OrderSubmitTime: DateTime.MinValue,
            TradeTime: DateTime.MinValue,
            Reason: "ORDER_NOT_FOUND",
            MarketCondition: MarketConditionAnalysis.Unknown(),
            ExecutionPath: new List<AuditEventRecord>().AsReadOnly(),
            AnalysisTimeMs: 0,
            IsRegulatorySafe: false
        );
    }

    public override string ToString()
    {
        return $"OrderId={OrderId}, Reason={Reason}, SafeToReport={IsRegulatorySafe}, AnalysisTime={AnalysisTimeMs:F2}ms";
    }
}

/// <summary>
/// Analysis of market conditions at the time of trade.
/// </summary>
public readonly record struct MarketConditionAnalysis(
    int RecentTickCount,
    DateTime LastTickTime,
    double PriceVolatility,
    double VolumeRegime
)
{
    public static MarketConditionAnalysis Stable() => new(0, DateTime.MinValue, 0.1, 0.5);
    public static MarketConditionAnalysis Unknown() => new(0, DateTime.MinValue, 0, 0);
}

/// <summary>
/// Summary of risk violations for an order.
/// </summary>
public readonly record struct RiskViolationSummary(
    long OrderId,
    int ViolationCount,
    IReadOnlyList<AuditEventRecord> Violations
);
