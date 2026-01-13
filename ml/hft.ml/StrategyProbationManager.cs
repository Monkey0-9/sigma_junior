// ML Alpha Integration - Strategy Probation Manager
// Part E: Strategy Lifecycle & Governance
// Manages probation period for new strategies before production approval

using System;
using System.Collections.Generic;
using System.Linq;

namespace Hft.Ml
{
    /// <summary>
    /// Probation status for a strategy.
    /// </summary>
    public enum ProbationStatus
    {
        /// <summary>Not in probation (legacy strategy or approved)</summary>
        Approved = 0,
        
        /// <summary>In active probation period</summary>
        InProbation = 1,
        
        /// <summary>Probation passed, approved for production</summary>
        ProbationPassed = 2,
        
        /// <summary>Probation failed, rejected from production</summary>
        ProbationFailed = 3,
        
        /// <summary>Probation revoked, returned to probation</summary>
        ProbationRevoked = 4
    }

    /// <summary>
    /// Probation metrics tracked during strategy vetting.
    /// </summary>
    public sealed record StrategyProbationMetrics
    {
        /// <summary>Strategy identifier</summary>
        public string StrategyId { get; init; } = string.Empty;
        
        /// <summary>Total trades executed during probation</summary>
        public long TotalTrades { get; init; }
        
        /// <summary>Win rate during probation</summary>
        public double WinRate { get; init; }
        
        /// <summary>Sharpe ratio during probation</summary>
        public double SharpeRatio { get; init; }
        
        /// <summary>Maximum drawdown during probation</summary>
        public double MaxDrawdown { get; init; }
        
        /// <summary>Out-of-sample Sharpe (if validation set available)</summary>
        public double? OutOfSampleSharpe { get; init; }
        
        /// <summary>Realized volatility during probation</summary>
        public double RealizedVolatility { get; init; }
        
        /// <summary>Number of risk violations during probation</summary>
        public int RiskViolationCount { get; init; }
        
        /// <summary>Average trade latency in milliseconds</summary>
        public double AvgTradeLatencyMs { get; init; }
        
        /// <summary>P99 trade latency in milliseconds</summary>
        public double P99TradeLatencyMs { get; init; }
    }

    /// <summary>
    /// Probation period record for strategy vetting.
    /// </summary>
    public sealed record StrategyProbationRecord
    {
        /// <summary>Strategy identifier</summary>
        public string StrategyId { get; init; } = string.Empty;
        
        /// <summary>Probation start date</summary>
        public DateTime StartDate { get; init; }
        
        /// <summary>Probation end date</summary>
        public DateTime EndDate { get; init; }
        
        /// <summary>Current probation status</summary>
        public ProbationStatus Status { get; init; }
        
        /// <summary>Probation duration in days</summary>
        public int DurationDays { get; init; }
        
        /// <summary>Minimum trade count requirement</summary>
        public long MinimumTradeCount { get; init; }
        
        /// <summary>Minimum Sharpe ratio requirement</summary>
        public double MinimumSharpeRatio { get; init; }
        
        /// <summary>Maximum drawdown tolerance</summary>
        public double MaxDrawdownTolerance { get; init; }
        
        /// <summary>Metrics collected during probation</summary>
        public StrategyProbationMetrics? Metrics { get; init; }
        
        /// <summary>Approval decision notes</summary>
        public string? ApprovalNotes { get; init; }
        
        /// <summary>Date of approval/rejection decision</summary>
        public DateTime? DecisionDate { get; init; }
        
        /// <summary>Approver/reviewer name</summary>
        public string? DecisionMaker { get; init; }
    }

    /// <summary>
    /// Probation violation record.
    /// </summary>
    public sealed record ProbationViolation
    {
        /// <summary>Violation type identifier</summary>
        public string ViolationType { get; init; } = string.Empty;
        
        /// <summary>Human-readable violation description</summary>
        public string Description { get; init; } = string.Empty;
        
        /// <summary>Violation severity (1-10)</summary>
        public int Severity { get; init; }
        
        /// <summary>Timestamp of violation</summary>
        public DateTime ViolationTime { get; init; }
        
        /// <summary>Detected metric that triggered violation</summary>
        public string MetricName { get; init; } = string.Empty;
        
        /// <summary>Actual value of metric</summary>
        public double ActualValue { get; init; }
        
        /// <summary>Threshold value that was exceeded</summary>
        public double ThresholdValue { get; init; }
    }

    /// <summary>
    /// Manages strategy probation periods and vetting.
    /// 
    /// Responsibilities:
    /// - Register new strategies in probation
    /// - Track probation metrics (trades, Sharpe, drawdown, latency)
    /// - Evaluate probation completion criteria
    /// - Generate approval/rejection decisions
    /// - Track probation violations
    /// 
    /// Thread safety: This is a reference type; use synchronized access if needed.
    /// </summary>
    public sealed class StrategyProbationManager : IDisposable
    {
        private readonly Dictionary<string, StrategyProbationRecord> _probationRecords;
        private readonly Dictionary<string, List<ProbationViolation>> _violations;
        
        /// <summary>Default probation duration in days</summary>
        public int DefaultProbationDays { get; set; } = 30;
        
        /// <summary>Minimum trades required for approval</summary>
        public long MinimumTradeCountForApproval { get; set; } = 100;
        
        /// <summary>Minimum Sharpe ratio for approval</summary>
        public double MinimumSharpeRatioForApproval { get; set; } = 0.5;
        
        /// <summary>Maximum drawdown tolerance for approval</summary>
        public double MaxDrawdownToleranceForApproval { get; set; } = 0.15; // 15% max drawdown

        /// <summary>Maximum P99 latency allowed (milliseconds)</summary>
        public double MaxP99LatencyMsForApproval { get; set; } = 50.0;

        /// <summary>Maximum allowed risk violations during probation</summary>
        public int MaxRiskViolationsForApproval { get; set; } = 3;

        public StrategyProbationManager()
        {
            _probationRecords = new Dictionary<string, StrategyProbationRecord>();
            _violations = new Dictionary<string, List<ProbationViolation>>();
        }

        /// <summary>
        /// Register a new strategy for probation.
        /// </summary>
        public StrategyProbationRecord RegisterStrategyForProbation(
            string strategyId,
            int probationDays = -1)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);
            
            if (probationDays < 0)
            {
                probationDays = DefaultProbationDays;
            }

            var startDate = DateTime.UtcNow;
            var endDate = startDate.AddDays(probationDays);

            var record = new StrategyProbationRecord
            {
                StrategyId = strategyId,
                StartDate = startDate,
                EndDate = endDate,
                Status = ProbationStatus.InProbation,
                DurationDays = probationDays,
                MinimumTradeCount = MinimumTradeCountForApproval,
                MinimumSharpeRatio = MinimumSharpeRatioForApproval,
                MaxDrawdownTolerance = MaxDrawdownToleranceForApproval
            };

            _probationRecords[strategyId] = record;
            if (!_violations.ContainsKey(strategyId))
            {
                _violations[strategyId] = new List<ProbationViolation>();
            }

            return record;
        }

        /// <summary>
        /// Record probation metrics for a strategy.
        /// </summary>
        public void RecordMetrics(
            string strategyId,
            long totalTrades,
            double winRate,
            double sharpeRatio,
            double maxDrawdown,
            double? outOfSampleSharpe,
            double realizedVolatility,
            int riskViolations,
            double avgTradeLatencyMs,
            double p99TradeLatencyMs)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);

            if (!_probationRecords.TryGetValue(strategyId, out var record))
            {
                throw new InvalidOperationException($"Strategy {strategyId} not in probation");
            }

            var metrics = new StrategyProbationMetrics
            {
                StrategyId = strategyId,
                TotalTrades = totalTrades,
                WinRate = winRate,
                SharpeRatio = sharpeRatio,
                MaxDrawdown = maxDrawdown,
                OutOfSampleSharpe = outOfSampleSharpe,
                RealizedVolatility = realizedVolatility,
                RiskViolationCount = riskViolations,
                AvgTradeLatencyMs = avgTradeLatencyMs,
                P99TradeLatencyMs = p99TradeLatencyMs
            };

            // Update record with metrics
            var updatedRecord = record with { Metrics = metrics };
            _probationRecords[strategyId] = updatedRecord;

            // Check for violations during recording
            CheckAndRecordViolations(strategyId, metrics);
        }

        /// <summary>
        /// Record a probation violation.
        /// </summary>
        public void RecordViolation(string strategyId, ProbationViolation violation)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);
            ArgumentNullException.ThrowIfNull(violation);

            if (!_violations.TryGetValue(strategyId, out var violations))
            {
                violations = new List<ProbationViolation>();
                _violations[strategyId] = violations;
            }

            violations.Add(violation);

            // If critical violations accumulate, auto-fail probation
            if (_violations[strategyId].Count > MaxRiskViolationsForApproval)
            {
                if (_probationRecords.TryGetValue(strategyId, out var record))
                {
                    var failedRecord = record with
                    {
                        Status = ProbationStatus.ProbationFailed,
                        DecisionDate = DateTime.UtcNow,
                        ApprovalNotes = $"Probation failed: {_violations[strategyId].Count} violations recorded"
                    };
                    _probationRecords[strategyId] = failedRecord;
                }
            }
        }

        /// <summary>
        /// Evaluate if a strategy has completed probation successfully.
        /// </summary>
        public bool EvaluateProbationCompletion(string strategyId)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);

            if (!_probationRecords.TryGetValue(strategyId, out var record))
            {
                return false; // Not in probation
            }

            if (record.Status != ProbationStatus.InProbation)
            {
                return record.Status == ProbationStatus.ProbationPassed;
            }

            // If probation period not over yet, cannot approve
            if (DateTime.UtcNow < record.EndDate)
            {
                return false;
            }

            // If no metrics collected, cannot approve
            if (record.Metrics == null)
            {
                return false;
            }

            // Check all criteria
            bool meetsTradeCount = record.Metrics.TotalTrades >= record.MinimumTradeCount;
            bool meetsSharpeRatio = record.Metrics.SharpeRatio >= record.MinimumSharpeRatio;
            bool meetsDrawdown = Math.Abs(record.Metrics.MaxDrawdown) <= record.MaxDrawdownTolerance;
            bool meetsLatency = record.Metrics.P99TradeLatencyMs <= MaxP99LatencyMsForApproval;
            bool meetsViolations = record.Metrics.RiskViolationCount <= MaxRiskViolationsForApproval;

            return meetsTradeCount && meetsSharpeRatio && meetsDrawdown && meetsLatency && meetsViolations;
        }

        /// <summary>
        /// Approve a strategy after successful probation.
        /// </summary>
        public StrategyProbationRecord ApproveProbation(
            string strategyId,
            string approverName,
            string? notes = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);
            ArgumentException.ThrowIfNullOrEmpty(approverName);

            if (!_probationRecords.TryGetValue(strategyId, out var record))
            {
                throw new InvalidOperationException($"Strategy {strategyId} not in probation");
            }

            var approvedRecord = record with
            {
                Status = ProbationStatus.ProbationPassed,
                DecisionDate = DateTime.UtcNow,
                DecisionMaker = approverName,
                ApprovalNotes = notes ?? "Probation completed successfully"
            };

            _probationRecords[strategyId] = approvedRecord;
            return approvedRecord;
        }

        /// <summary>
        /// Reject a strategy during probation.
        /// </summary>
        public StrategyProbationRecord RejectProbation(
            string strategyId,
            string reviewerName,
            string reason)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);
            ArgumentException.ThrowIfNullOrEmpty(reviewerName);
            ArgumentException.ThrowIfNullOrEmpty(reason);

            if (!_probationRecords.TryGetValue(strategyId, out var record))
            {
                throw new InvalidOperationException($"Strategy {strategyId} not in probation");
            }

            var rejectedRecord = record with
            {
                Status = ProbationStatus.ProbationFailed,
                DecisionDate = DateTime.UtcNow,
                DecisionMaker = reviewerName,
                ApprovalNotes = reason
            };

            _probationRecords[strategyId] = rejectedRecord;
            return rejectedRecord;
        }

        /// <summary>
        /// Revoke approval and return strategy to probation.
        /// </summary>
        public StrategyProbationRecord RevokeProbationApproval(
            string strategyId,
            string reviewerName,
            string reason)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);
            ArgumentException.ThrowIfNullOrEmpty(reviewerName);
            ArgumentException.ThrowIfNullOrEmpty(reason);

            if (!_probationRecords.TryGetValue(strategyId, out var record))
            {
                throw new InvalidOperationException($"Strategy {strategyId} not found");
            }

            var revokedRecord = record with
            {
                Status = ProbationStatus.ProbationRevoked,
                DecisionDate = DateTime.UtcNow,
                DecisionMaker = reviewerName,
                ApprovalNotes = $"Approval revoked: {reason}"
            };

            _probationRecords[strategyId] = revokedRecord;
            return revokedRecord;
        }

        /// <summary>
        /// Get probation record for a strategy.
        /// </summary>
        public StrategyProbationRecord? GetProbationRecord(string strategyId)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);
            _probationRecords.TryGetValue(strategyId, out var record);
            return record;
        }

        /// <summary>
        /// Get violations for a strategy.
        /// </summary>
        public IReadOnlyList<ProbationViolation> GetViolations(string strategyId)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);
            if (_violations.TryGetValue(strategyId, out var vios))
            {
                return vios.AsReadOnly();
            }
            return Array.Empty<ProbationViolation>();
        }

        /// <summary>
        /// Get all strategies in probation.
        /// </summary>
        public IReadOnlyList<StrategyProbationRecord> GetStrategiesInProbation()
        {
            return _probationRecords.Values
                .Where(r => r.Status == ProbationStatus.InProbation)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Get all approved strategies.
        /// </summary>
        public IReadOnlyList<StrategyProbationRecord> GetApprovedStrategies()
        {
            return _probationRecords.Values
                .Where(r => r.Status == ProbationStatus.ProbationPassed)
                .ToList()
                .AsReadOnly();
        }

        private void CheckAndRecordViolations(string strategyId, StrategyProbationMetrics metrics)
        {
            // Check for excessive drawdown
            if (metrics.MaxDrawdown > MaxDrawdownToleranceForApproval * 1.5)
            {
                RecordViolation(strategyId, new ProbationViolation
                {
                    ViolationType = "EXCESSIVE_DRAWDOWN",
                    Description = "Strategy experienced severe drawdown during probation",
                    Severity = 8,
                    ViolationTime = DateTime.UtcNow,
                    MetricName = "MaxDrawdown",
                    ActualValue = metrics.MaxDrawdown,
                    ThresholdValue = MaxDrawdownToleranceForApproval * 1.5
                });
            }

            // Check for insufficient Sharpe
            if (metrics.SharpeRatio < MinimumSharpeRatioForApproval * 0.5)
            {
                RecordViolation(strategyId, new ProbationViolation
                {
                    ViolationType = "LOW_SHARPE_RATIO",
                    Description = "Strategy Sharpe ratio well below minimum threshold",
                    Severity = 7,
                    ViolationTime = DateTime.UtcNow,
                    MetricName = "SharpeRatio",
                    ActualValue = metrics.SharpeRatio,
                    ThresholdValue = MinimumSharpeRatioForApproval * 0.5
                });
            }

            // Check for high latency
            if (metrics.P99TradeLatencyMs > MaxP99LatencyMsForApproval * 1.5)
            {
                RecordViolation(strategyId, new ProbationViolation
                {
                    ViolationType = "HIGH_LATENCY",
                    Description = "Strategy exhibits high P99 trade latency",
                    Severity = 6,
                    ViolationTime = DateTime.UtcNow,
                    MetricName = "P99TradeLatencyMs",
                    ActualValue = metrics.P99TradeLatencyMs,
                    ThresholdValue = MaxP99LatencyMsForApproval * 1.5
                });
            }

            // Check for risk violations
            if (metrics.RiskViolationCount > MaxRiskViolationsForApproval)
            {
                RecordViolation(strategyId, new ProbationViolation
                {
                    ViolationType = "RISK_VIOLATIONS",
                    Description = "Strategy exceeded maximum allowed risk violations",
                    Severity = 9,
                    ViolationTime = DateTime.UtcNow,
                    MetricName = "RiskViolationCount",
                    ActualValue = metrics.RiskViolationCount,
                    ThresholdValue = MaxRiskViolationsForApproval
                });
            }
        }

        public void Dispose()
        {
            _probationRecords.Clear();
            _violations.Clear();
        }
    }
}
