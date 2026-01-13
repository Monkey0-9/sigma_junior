using System;
using System.Collections.Generic;

namespace Hft.Ml.FeatureStore
{
    public readonly record struct FeatureSetDefinition(
        string FeatureSetId,
        Version Version,
        IReadOnlyList<string> FeatureNames,
        string Checksum,
        string Owner,
        DateTime CreatedAt,
        string Description,
        int MaxLookbackPeriods,
        bool IsRealTime
    );

    public readonly record struct FeatureValue(
        string FeatureSetId,
        long InstrumentId,
        string FeatureName,
        double Value,
        long AsOfTimestamp,
        long ComputedTimestamp,
        string Source,
        string ComputationVersion
    );

    public readonly record struct FeatureVector(
        FeatureSetDefinition Definition,
        long InstrumentId,
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "DTO")]
        double[] Values,
        long AsOfTimestamp,
        long ComputedTimestamp,
        string Checksum
    );

    public readonly record struct FeatureRequest(string FeatureSetId, long InstrumentId, long AsOfTimestamp, int TimeoutMicroseconds, string RequestId);

    public interface IFeatureStore
    {
        FeatureVector GetFeatures(FeatureRequest request);
        IReadOnlyList<FeatureVector> GetHistoricalFeatures(string featureSetId, long instrumentId, DateTime startTime, DateTime endTime, TimeSpan interval);
        void RegisterFeatureSet(FeatureSetDefinition definition);
        void StoreFeatures(FeatureValue feature);
        void StoreFeaturesBatch(IEnumerable<FeatureValue> features);
        FeatureSetDefinition? GetFeatureSet(string featureSetId);
    }
}
