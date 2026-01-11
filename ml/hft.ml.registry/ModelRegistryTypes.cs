// ML Alpha Integration - Model Registry Types
// Production-ready model versioning with full audit trail

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Hft.Ml.Registry
{
    /// <summary>
    /// Model stage in lifecycle.
    /// </summary>
    public enum ModelStage
    {
        /// <summary>Model is being researched/developed</summary>
        Research = 0,
        
        /// <summary>Model trained, awaiting validation</summary>
        TrainingComplete = 1,
        
        /// <summary>Model passed validation, ready for deployment</summary>
        ValidationPassed = 2,
        
        /// <summary>Model deployed to staging environment</summary>
        Staging = 3,
        
        /// <summary>Model actively serving production traffic</summary>
        Production = 4,
        
        /// <summary>Model deprecated, no longer serving</summary>
        Deprecated = 5,
        
        /// <summary>Model archived for audit/compliance</summary>
        Archived = 6
    }

    /// <summary>
    /// Model metadata for registry entries.
    /// Contains all information needed for reproducibility and audit.
    /// </summary>
    public readonly struct ModelMetadata
    {
        /// <summary>Unique model identifier (e.g., "momentum-classifier-v1")</summary>
        public string ModelId { get; }
        
        /// <summary>Semantic version</summary>
        public Version Version { get; }
        
        /// <summary>Model stage in lifecycle</summary>
        public ModelStage Stage { get; }
        
        /// <summary>Feature set used for training</summary>
        public string FeatureSetId { get; }
        
        /// <summary>Feature set version at training time</summary>
        public Version FeatureSetVersion { get; }
        
        /// <summary>Hash of training data for verification</summary>
        public string TrainingDataHash { get; }
        
        /// <summary>Path to training data source</summary>
        public string TrainingDataPath { get; }
        
        /// <summary>Training start timestamp</summary>
        public DateTime TrainingStartTime { get; }
        
        /// <summary>Training end timestamp</summary>
        public DateTime TrainingEndTime { get; }
        
        /// <summary>Model framework (e.g., "sklearn", "pytorch", "onnx")</summary>
        public string Framework { get; }
        
        /// <summary>Framework version</summary>
        public string FrameworkVersion { get; }
        
        /// <summary>Hyperparameters used (JSON serialized)</summary>
        public string Hyperparameters { get; }
        
        /// <summary>Training metrics (accuracy, AUC, etc.)</summary>
        public Dictionary<string, double> TrainingMetrics { get; }
        
        /// <summary>Cross-validation results</summary>
        public CrossValidationResults CrossValidationResults { get; }
        
        /// <summary>Model checksum (SHA-256 of model file)</summary>
        public string ModelChecksum { get; }
        
        /// <summary>Model file path in storage</summary>
        public string ModelStoragePath { get; }
        
        /// <summary>Owner/team responsible</summary>
        public string Owner { get; }
        
        /// <summary>Approver for production deployment</summary>
        public string? ApprovedBy { get; }
        
        /// <summary>Approval timestamp</summary>
        public DateTime? ApprovedAt { get; }
        
        /// <summary>Description and purpose</summary>
        public string Description { get; }
        
        /// <summary>Risk assessment notes</summary>
        public string RiskNotes { get; }
        
        /// <summary>Tags for categorization</summary>
        public List<string> Tags { get; }
        
        /// <summary>Creation timestamp</summary>
        public DateTime CreatedAt { get; }
        
        /// <summary>Last update timestamp</summary>
        public DateTime UpdatedAt { get; }
    }

    /// <summary>
    /// Cross-validation results for model validation.
    /// </summary>
    public readonly struct CrossValidationResults
    {
        /// <summary>Number of folds</summary>
        public int NumFolds { get; }
        
        /// <summary>Mean accuracy across folds</summary>
        public double MeanAccuracy { get; }
        
        /// <summary>Standard deviation of accuracy</summary>
        public double StdAccuracy { get; }
        
        /// <summary>Mean AUC-ROC across folds</summary>
        public double MeanAuc { get; }
        
        /// <summary>Mean Sharpe ratio (for trading models)</summary>
        public double MeanSharpe { get; }
        
        /// <summary>Max drawdown observed</summary>
        public double MaxDrawdown { get; }
        
        /// <summary>Win rate percentage</summary>
        public double WinRate { get; }
        
        /// <summary>Mean return per trade</summary>
        public double MeanReturnPerTrade { get; }
        
        /// <summary>Results per fold</summary>
        public Dictionary<int, FoldResult> FoldResults { get; }
    }

    /// <summary>
    /// Results for a single cross-validation fold.
    /// </summary>
    public readonly struct FoldResult
    {
        public int FoldNumber { get; }
        public double Accuracy { get; }
        public double Auc { get; }
        public double Sharpe { get; }
        public double Return { get; }
        public double Drawdown { get; }
    }

    /// <summary>
    /// Backtest results for model validation.
    /// </summary>
    public readonly struct BacktestResults
    {
        /// <summary>Backtest period start</summary>
        public DateTime PeriodStart { get; }
        
        /// <summary>Backtest period end</summary>
        public DateTime PeriodEnd { get; }
        
        /// <summary>Total return</summary>
        public double TotalReturn { get; }
        
        /// <summary>Annualized return</summary>
        public double AnnualizedReturn { get; }
        
        /// <summary>Annualized volatility</summary>
        public double AnnualizedVolatility { get; }
        
        /// <summary>Sharpe ratio</summary>
        public double SharpeRatio { get; }
        
        /// <summary>Maximum drawdown</summary>
        public double MaxDrawdown { get; }
        
        /// <summary>Sortino ratio</summary>
        public double SortinoRatio { get; }
        
        /// <summary>Calmar ratio</summary>
        public double CalmarRatio { get; }
        
        /// <summary>Total number of trades</summary>
        public int TotalTrades { get; }
        
        /// <summary>Win rate</summary>
        public double WinRate { get; }
        
        /// <summary>Average trade duration</summary>
        public TimeSpan AvgTradeDuration { get; }
        
        /// <summary>Profit factor</summary>
        public double ProfitFactor { get; }
        
        /// <summary>Tail risk metrics</summary>
        public TailRiskMetrics TailRisk { get; }
        
        /// <summary>Monthly returns breakdown</summary>
        public Dictionary<string, double> MonthlyReturns { get; }
    }

    /// <summary>
    /// Tail risk metrics for risk assessment.
    /// </summary>
    public readonly struct TailRiskMetrics
    {
        public double Var95 { get; }
        public double Var99 { get; }
        public double CVar95 { get; }
        public double CVar99 { get; }
        public double Skewness { get; }
        public double Kurtosis { get; }
        public double ExpectedShortfall { get; }
    }

    /// <summary>
    /// Model validation checklist results.
    /// </summary>
    public readonly struct ModelValidationChecklist
    {
        /// <summary>Statistical tests passed</summary>
        public StatisticalTestsResult StatisticalTests { get; }
        
        /// <summary>PnL attribution passed</summary>
        public PnlAttributionResult PnlAttribution { get; }
        
        /// <summary>Overfit detection passed</summary>
        public OverfitCheckResult OverfitCheck { get; }
        
        /// <summary>Latency requirements met</summary>
        public LatencyCheckResult LatencyCheck { get; }
        
        /// <summary>Explainability requirements met</summary>
        public ExplainabilityCheckResult ExplainabilityCheck { get; }
        
        /// <summary>Overall validation passed</summary>
        public bool AllChecksPassed { get; }
        
        /// <summary>Validation notes and issues</summary>
        public List<string> ValidationNotes { get; }
        
        /// <summary>Validation timestamp</summary>
        public DateTime ValidatedAt { get; }
        
        /// <summary>Validator identifier</summary>
        public string ValidatedBy { get; }
    }

    public readonly struct StatisticalTestsResult
    {
        public bool StationarityTestPassed { get; }
        public bool NormalityTestPassed { get; }
        public bool AutocorrelationTestPassed { get; }
        public double PValue { get; }
    }

    public readonly struct PnlAttributionResult
    {
        public bool AttributionPassed { get; }
        public double InformationCoefficient { get; }
        public double FactorExposure { get; }
        public double UniqueReturn { get; }
    }

    public readonly struct OverfitCheckResult
    {
        public bool OverfitCheckPassed { get; }
        public double TrainScore { get; }
        public double TestScore { get; }
        public double ScoreGap { get; }
        public bool IsSignificant { get; }
    }

    public readonly struct LatencyCheckResult
    {
        public bool LatencyCheckPassed { get; }
        public double P50LatencyUs { get; }
        public double P99LatencyUs { get; }
        public double MaxLatencyUs { get; }
        public bool MeetsSla { get; }
    }

    public readonly struct ExplainabilityCheckResult
    {
        public bool ExplainabilityCheckPassed { get; }
        public bool FeatureImportanceAvailable { get; }
        public bool ShapValuesAvailable { get; }
        public string ExplanationReportPath { get; }
    }
}

