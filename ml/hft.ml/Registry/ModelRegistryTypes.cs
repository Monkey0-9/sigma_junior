using System;
using System.Collections.Generic;

namespace Hft.Ml.Registry
{
    public enum ModelStage { Research, TrainingComplete, ValidationPassed, Staging, Production, Deprecated, Archived }

    public readonly record struct ModelMetadata(
        string ModelId,
        Version Version,
        ModelStage Stage,
        string FeatureSetId,
        Version FeatureSetVersion,
        string TrainingDataHash,
        string TrainingDataPath,
        DateTime TrainingStartTime,
        DateTime TrainingEndTime,
        string Framework,
        string FrameworkVersion,
        string Hyperparameters,
        IReadOnlyDictionary<string, double> TrainingMetrics,
        CrossValidationResults CrossValidationResults,
        string ModelChecksum,
        string ModelStoragePath,
        string Owner,
        string? ApprovedBy,
        DateTime? ApprovedAt,
        string Description,
        string RiskNotes,
        IReadOnlyList<string> Tags,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );

    public readonly record struct CrossValidationResults(
        int NumFolds,
        double MeanAccuracy,
        double StdAccuracy,
        double MeanAuc,
        double MeanSharpe,
        double MaxDrawdown,
        double WinRate,
        double MeanReturnPerTrade,
        IReadOnlyDictionary<int, FoldResult> FoldResults
    );

    public readonly record struct FoldResult(int FoldNumber, double Accuracy, double Auc, double Sharpe, double Return, double Drawdown);

    public readonly record struct BacktestResults(
        DateTime PeriodStart,
        DateTime PeriodEnd,
        double TotalReturn,
        double AnnualizedReturn,
        double AnnualizedVolatility,
        double SharpeRatio,
        double MaxDrawdown,
        double SortinoRatio,
        double CalmarRatio,
        int TotalTrades,
        double WinRate,
        TimeSpan AvgTradeDuration,
        double ProfitFactor,
        TailRiskMetrics TailRisk,
        IReadOnlyDictionary<string, double> MonthlyReturns
    );

    public readonly record struct TailRiskMetrics(double Var95, double Var99, double CVar95, double CVar99, double Skewness, double Kurtosis, double ExpectedShortfall);

    public readonly record struct ModelValidationChecklist(
        StatisticalTestsResult StatisticalTests,
        PnlAttributionResult PnlAttribution,
        OverfitCheckResult OverfitCheck,
        LatencyCheckResult LatencyCheck,
        ExplainabilityCheckResult ExplainabilityCheck,
        bool AllChecksPassed,
        IReadOnlyList<string> ValidationNotes,
        DateTime ValidatedAt,
        string ValidatedBy
    );

    public readonly record struct StatisticalTestsResult(bool StationarityTestPassed, bool NormalityTestPassed, bool AutocorrelationTestPassed, double PValue);
    public readonly record struct PnlAttributionResult(bool AttributionPassed, double InformationCoefficient, double FactorExposure, double UniqueReturn);
    public readonly record struct OverfitCheckResult(bool OverfitCheckPassed, double TrainScore, double TestScore, double ScoreGap, bool IsSignificant);
    public readonly record struct LatencyCheckResult(bool LatencyCheckPassed, double P50LatencyUs, double P99LatencyUs, double MaxLatencyUs, bool MeetsSla);
    public readonly record struct ExplainabilityCheckResult(bool ExplainabilityCheckPassed, bool FeatureImportanceAvailable, bool ShapValuesAvailable, string ExplanationReportPath);
}
