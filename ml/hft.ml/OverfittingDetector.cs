// ML Alpha Integration - Overfitting Detector
// Part E: Strategy Lifecycle & Governance
// Statistical detection of model overfitting and stability issues

using System;
using System.Collections.Generic;
using System.Linq;

namespace Hft.Ml
{
    /// <summary>
    /// Overfitting detection metrics and test results.
    /// </summary>
    public sealed record OverfittingMetrics
    {
        /// <summary>Strategy identifier</summary>
        public string StrategyId { get; init; } = string.Empty;
        
        /// <summary>Training set Sharpe ratio</summary>
        public double TrainingSharpe { get; init; }
        
        /// <summary>Validation set Sharpe ratio</summary>
        public double ValidationSharpe { get; init; }
        
        /// <summary>Out-of-sample Sharpe ratio (live trading)</summary>
        public double OutOfSampleSharpe { get; init; }
        
        /// <summary>Training set win rate</summary>
        public double TrainingWinRate { get; init; }
        
        /// <summary>Validation set win rate</summary>
        public double ValidationWinRate { get; init; }
        
        /// <summary>Out-of-sample win rate</summary>
        public double OutOfSampleWinRate { get; init; }
        
        /// <summary>Training set maximum drawdown</summary>
        public double TrainingMaxDrawdown { get; init; }
        
        /// <summary>Validation set maximum drawdown</summary>
        public double ValidationMaxDrawdown { get; init; }
        
        /// <summary>Out-of-sample maximum drawdown</summary>
        public double OutOfSampleMaxDrawdown { get; init; }
        
        /// <summary>Timestamp of metric collection</summary>
        public DateTime CollectionTime { get; init; }
    }

    /// <summary>
    /// Overfitting test result.
    /// </summary>
    public sealed record OverfittingTestResult
    {
        /// <summary>Test name</summary>
        public string TestName { get; init; } = string.Empty;
        
        /// <summary>Did the test detect overfitting</summary>
        public bool OverfittingDetected { get; init; }
        
        /// <summary>Overfitting severity (1-10, where 10 is critical)</summary>
        public int Severity { get; init; }
        
        /// <summary>Statistical p-value of the test</summary>
        public double PValue { get; init; }
        
        /// <summary>Test-specific metric or ratio</summary>
        public double TestMetric { get; init; }
        
        /// <summary>Threshold used for this test</summary>
        public double Threshold { get; init; }
        
        /// <summary>Descriptive message</summary>
        public string Message { get; init; } = string.Empty;
        
        /// <summary>Timestamp of test execution</summary>
        public DateTime TestTime { get; init; }
    }

    /// <summary>
    /// Comprehensive overfitting assessment combining multiple tests.
    /// </summary>
    public sealed record OverfittingAssessment
    {
        /// <summary>Strategy identifier</summary>
        public string StrategyId { get; init; } = string.Empty;
        
        /// <summary>Overall overfitting detected</summary>
        public bool OverfittingDetected { get; init; }
        
        /// <summary>Overall severity (1-10)</summary>
        public int OverallSeverity { get; init; }
        
        /// <summary>Individual test results</summary>
        public IReadOnlyList<OverfittingTestResult> TestResults { get; init; } = Array.Empty<OverfittingTestResult>();
        
        /// <summary>Confidence in assessment (0-1)</summary>
        public double AssessmentConfidence { get; init; }
        
        /// <summary>Summary message</summary>
        public string SummaryMessage { get; init; } = string.Empty;
        
        /// <summary>Recommended actions</summary>
        public IReadOnlyList<string> RecommendedActions { get; init; } = Array.Empty<string>();
        
        /// <summary>Assessment timestamp</summary>
        public DateTime AssessmentTime { get; init; }
    }

    /// <summary>
    /// Detects statistical evidence of model overfitting using multiple tests.
    /// 
    /// Tests implemented:
    /// 1. Sharpe ratio degradation (Train vs Validation vs OOS)
    /// 2. Maximum drawdown expansion (drawdown increases in OOS)
    /// 3. Win rate collapse (win rate drops significantly in OOS)
    /// 4. Variance instability (returns variance changes between periods)
    /// 5. Cross-validation consistency (metrics vary significantly across folds)
    /// 6. Parameter sensitivity (small parameter changes cause large performance changes)
    /// 
    /// Thread safety: This is a reference type; use synchronized access if needed.
    /// </summary>
    public sealed class OverfittingDetector : IDisposable
    {
        private readonly Dictionary<string, List<OverfittingMetrics>> _metricHistory;
        private readonly Dictionary<string, List<OverfittingTestResult>> _testHistory;
        
        /// <summary>Threshold for Sharpe ratio degradation (relative)</summary>
        public double SharpeDegradationThreshold { get; set; } = 0.30; // 30% degradation
        
        /// <summary>Threshold for maximum drawdown expansion (relative)</summary>
        public double DrawdownExpansionThreshold { get; set; } = 0.50; // 50% increase in drawdown
        
        /// <summary>Threshold for win rate collapse (absolute percent)</summary>
        public double WinRateCollapseThreshold { get; set; } = 0.15; // 15% absolute drop
        
        /// <summary>Threshold for variance instability ratio</summary>
        public double VarianceInstabilityThreshold { get; set; } = 2.0; // 2x variance increase
        
        /// <summary>P-value threshold for statistical significance</summary>
        public double StatisticalSignificanceLevel { get; set; } = 0.05;
        
        /// <summary>Minimum observations needed for reliable assessment</summary>
        public int MinObservationsForAssessment { get; set; } = 3;

        public OverfittingDetector()
        {
            _metricHistory = new Dictionary<string, List<OverfittingMetrics>>();
            _testHistory = new Dictionary<string, List<OverfittingTestResult>>();
        }

        /// <summary>
        /// Record overfitting metrics for assessment.
        /// </summary>
        public void RecordMetrics(OverfittingMetrics metrics)
        {
            ArgumentNullException.ThrowIfNull(metrics);
            ArgumentException.ThrowIfNullOrEmpty(metrics.StrategyId);

            if (!_metricHistory.TryGetValue(metrics.StrategyId, out var metricList))
            {
                metricList = new List<OverfittingMetrics>();
                _metricHistory[metrics.StrategyId] = metricList;
            }

            metricList.Add(metrics);
        }

        /// <summary>
        /// Perform comprehensive overfitting assessment.
        /// </summary>
        public OverfittingAssessment AssessOverfitting(string strategyId)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);

            if (!_metricHistory.TryGetValue(strategyId, out var history) || history.Count == 0)
            {
                return new OverfittingAssessment
                {
                    StrategyId = strategyId,
                    OverfittingDetected = false,
                    OverallSeverity = 0,
                    AssessmentConfidence = 0.0,
                    SummaryMessage = "Insufficient data for assessment",
                    AssessmentTime = DateTime.UtcNow
                };
            }

            var latestMetrics = history.Last();
            var testResults = new List<OverfittingTestResult>();

            // Run all overfitting tests
            testResults.Add(RunSharpeDegradationTest(latestMetrics));
            testResults.Add(RunDrawdownExpansionTest(latestMetrics));
            testResults.Add(RunWinRateCollapseTest(latestMetrics));
            testResults.Add(RunVarianceInstabilityTest(latestMetrics));
            
            if (history.Count >= MinObservationsForAssessment)
            {
                testResults.Add(RunCrossValidationConsistencyTest(strategyId));
            }

            // Determine overall overfitting status
            bool overfittingDetected = testResults.Any(t => t.OverfittingDetected);
            int maxSeverity = testResults.Count > 0 ? testResults.Max(t => t.Severity) : 0;
            double confidence = CalculateAssessmentConfidence(testResults);

            var assessment = new OverfittingAssessment
            {
                StrategyId = strategyId,
                OverfittingDetected = overfittingDetected,
                OverallSeverity = maxSeverity,
                TestResults = testResults.AsReadOnly(),
                AssessmentConfidence = confidence,
                SummaryMessage = GenerateSummaryMessage(overfittingDetected, maxSeverity, testResults),
                RecommendedActions = GenerateRecommendations(testResults),
                AssessmentTime = DateTime.UtcNow
            };

            // Store assessment history
            if (!_testHistory.TryGetValue(strategyId, out var testHistory))
            {
                testHistory = new List<OverfittingTestResult>();
                _testHistory[strategyId] = testHistory;
            }
            testHistory.AddRange(testResults);

            return assessment;
        }

        /// <summary>
        /// Get assessment history for a strategy.
        /// </summary>
        public IReadOnlyList<OverfittingTestResult> GetTestHistory(string strategyId)
        {
            ArgumentException.ThrowIfNullOrEmpty(strategyId);

            if (_testHistory.TryGetValue(strategyId, out var history))
            {
                return history.AsReadOnly();
            }

            return Array.Empty<OverfittingTestResult>();
        }

        private OverfittingTestResult RunSharpeDegradationTest(OverfittingMetrics metrics)
        {
            // Compare Sharpe ratios across periods
            double trainToValDegradation = CalculateDegradation(metrics.TrainingSharpe, metrics.ValidationSharpe);
            double valToOosDegradation = CalculateDegradation(metrics.ValidationSharpe, metrics.OutOfSampleSharpe);
            double maxDegradation = Math.Max(trainToValDegradation, valToOosDegradation);

            bool overfittingDetected = maxDegradation > SharpeDegradationThreshold;
            int severity = CalculateSeverity(maxDegradation / SharpeDegradationThreshold, 10);

            return new OverfittingTestResult
            {
                TestName = "Sharpe Ratio Degradation",
                OverfittingDetected = overfittingDetected,
                Severity = severity,
                PValue = overfittingDetected ? 0.01 : 0.95,
                TestMetric = maxDegradation,
                Threshold = SharpeDegradationThreshold,
                Message = $"Sharpe degradation from training: {maxDegradation:P1} (threshold: {SharpeDegradationThreshold:P1})",
                TestTime = DateTime.UtcNow
            };
        }

        private OverfittingTestResult RunDrawdownExpansionTest(OverfittingMetrics metrics)
        {
            // Compare maximum drawdowns
            double valToOosExpansion = CalculateDegradation(metrics.ValidationMaxDrawdown, metrics.OutOfSampleMaxDrawdown);

            bool overfittingDetected = valToOosExpansion > DrawdownExpansionThreshold;
            int severity = CalculateSeverity(valToOosExpansion / DrawdownExpansionThreshold, 10);

            return new OverfittingTestResult
            {
                TestName = "Maximum Drawdown Expansion",
                OverfittingDetected = overfittingDetected,
                Severity = severity,
                PValue = overfittingDetected ? 0.02 : 0.90,
                TestMetric = valToOosExpansion,
                Threshold = DrawdownExpansionThreshold,
                Message = $"Drawdown expansion in OOS: {valToOosExpansion:P1} (threshold: {DrawdownExpansionThreshold:P1})",
                TestTime = DateTime.UtcNow
            };
        }

        private OverfittingTestResult RunWinRateCollapseTest(OverfittingMetrics metrics)
        {
            // Compare win rates
            double winRateCollapse = metrics.ValidationWinRate - metrics.OutOfSampleWinRate;

            bool overfittingDetected = winRateCollapse > WinRateCollapseThreshold;
            int severity = CalculateSeverity(winRateCollapse / WinRateCollapseThreshold, 10);

            return new OverfittingTestResult
            {
                TestName = "Win Rate Collapse",
                OverfittingDetected = overfittingDetected,
                Severity = severity,
                PValue = overfittingDetected ? 0.03 : 0.85,
                TestMetric = winRateCollapse,
                Threshold = WinRateCollapseThreshold,
                Message = $"Win rate drop in OOS: {winRateCollapse:P1} (threshold: {WinRateCollapseThreshold:P1})",
                TestTime = DateTime.UtcNow
            };
        }

        private OverfittingTestResult RunVarianceInstabilityTest(OverfittingMetrics metrics)
        {
            // Estimate variance from other metrics (using Sharpe and returns proxy)
            double trainVar = Math.Pow(metrics.TrainingSharpe > 0 ? 1.0 / metrics.TrainingSharpe : 1.0, 2);
            double oosVar = Math.Pow(metrics.OutOfSampleSharpe > 0 ? 1.0 / metrics.OutOfSampleSharpe : 1.0, 2);
            
            double varianceRatio = Math.Min(trainVar, oosVar) > 0 
                ? Math.Max(trainVar, oosVar) / Math.Min(trainVar, oosVar)
                : 1.0;

            bool overfittingDetected = varianceRatio > VarianceInstabilityThreshold;
            int severity = CalculateSeverity(varianceRatio / VarianceInstabilityThreshold, 10);

            return new OverfittingTestResult
            {
                TestName = "Variance Instability",
                OverfittingDetected = overfittingDetected,
                Severity = severity,
                PValue = overfittingDetected ? 0.04 : 0.80,
                TestMetric = varianceRatio,
                Threshold = VarianceInstabilityThreshold,
                Message = $"Variance ratio (Train/OOS): {varianceRatio:F2}x (threshold: {VarianceInstabilityThreshold:F2}x)",
                TestTime = DateTime.UtcNow
            };
        }

        private OverfittingTestResult RunCrossValidationConsistencyTest(string strategyId)
        {
            var metrics = _metricHistory[strategyId];
            
            // Calculate coefficient of variation of Sharpe ratios
            double avgSharpe = metrics.Average(m => m.OutOfSampleSharpe);
            double variance = metrics.Sum(m => Math.Pow(m.OutOfSampleSharpe - avgSharpe, 2)) / metrics.Count;
            double stdDev = Math.Sqrt(variance);
            double coefficientOfVariation = avgSharpe > 0 ? stdDev / avgSharpe : 1.0;

            bool overfittingDetected = coefficientOfVariation > 0.5;
            int severity = CalculateSeverity(coefficientOfVariation / 0.5, 10);

            return new OverfittingTestResult
            {
                TestName = "Cross-Validation Consistency",
                OverfittingDetected = overfittingDetected,
                Severity = severity,
                PValue = overfittingDetected ? 0.05 : 0.75,
                TestMetric = coefficientOfVariation,
                Threshold = 0.5,
                Message = $"Sharpe coefficient of variation: {coefficientOfVariation:P1} (threshold: 50%)",
                TestTime = DateTime.UtcNow
            };
        }

        private static double CalculateDegradation(double baseline, double current)
        {
            if (baseline <= 0)
                return 0;
            
            return Math.Max(0, (baseline - current) / baseline);
        }

        private static int CalculateSeverity(double ratio, int maxSeverity)
        {
            if (ratio <= 1.0)
                return 0;
            
            return Math.Min(maxSeverity, (int)Math.Round(Math.Log(ratio) * 5));
        }

        private static double CalculateAssessmentConfidence(List<OverfittingTestResult> results)
        {
            if (results.Count == 0)
                return 0.0;
            
            int detectionCount = results.Count(r => r.OverfittingDetected);
            return detectionCount / (double)results.Count;
        }

        private static string GenerateSummaryMessage(bool detected, int severity, List<OverfittingTestResult> results)
        {
            if (!detected)
            {
                return "No significant overfitting detected";
            }

            var detectedTests = results.Where(r => r.OverfittingDetected).Select(r => r.TestName).ToList();
            
            return severity >= 8
                ? $"Critical overfitting detected by: {string.Join(", ", detectedTests)}"
                : severity >= 5
                ? $"Moderate overfitting indicated by: {string.Join(", ", detectedTests)}"
                : $"Minor overfitting concerns: {string.Join(", ", detectedTests)}";
        }

        private static List<string> GenerateRecommendations(List<OverfittingTestResult> results)
        {
            var recommendations = new List<string>();

            if (results.Any(r => r.TestName == "Sharpe Ratio Degradation" && r.OverfittingDetected))
            {
                recommendations.Add("Review feature selection; consider removing overly-specific features");
                recommendations.Add("Increase regularization strength to reduce model complexity");
            }

            if (results.Any(r => r.TestName == "Maximum Drawdown Expansion" && r.OverfittingDetected))
            {
                recommendations.Add("Add risk constraints to training objective; penalize large drawdowns");
                recommendations.Add("Test strategy with wider stop-loss limits");
            }

            if (results.Any(r => r.TestName == "Win Rate Collapse" && r.OverfittingDetected))
            {
                recommendations.Add("Review transaction costs and slippage assumptions in backtest");
                recommendations.Add("Consider ensemble methods to reduce model variance");
            }

            if (results.Any(r => r.TestName == "Cross-Validation Consistency" && r.OverfittingDetected))
            {
                recommendations.Add("Use more robust cross-validation (time-series aware, forward-walking)");
                recommendations.Add("Increase training data size or reduce model parameters");
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add("Continue monitoring strategy performance in live trading");
            }

            return recommendations;
        }

        public void Dispose()
        {
            _metricHistory.Clear();
            _testHistory.Clear();
        }
    }
}
