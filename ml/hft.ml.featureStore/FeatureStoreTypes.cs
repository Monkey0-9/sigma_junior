// ML Alpha Integration - Feature Store Types
// Provides time-travel semantics and feature versioning for ML pipelines

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Hft.Core;

namespace Hft.Ml.FeatureStore
{
    /// <summary>
    /// Feature set definition for a model.
    /// Immutable once registered - ensures training-serving consistency.
    /// </summary>
    public readonly struct FeatureSetDefinition
    {
        /// <summary>Unique identifier for this feature set</summary>
        public string FeatureSetId { get; }
        
        /// <summary>Semantic version of the feature set</summary>
        public Version Version { get; }
        
        /// <summary>List of feature names in order</summary>
        public IReadOnlyList<string> FeatureNames { get; }
        
        /// <summary>Hash of feature definitions for integrity verification</summary>
        public string Checksum { get; }
        
        /// <summary>Owner/team responsible for this feature set</summary>
        public string Owner { get; }
        
        /// <summary>Timestamp when feature set was created</summary>
        public DateTime CreatedAt { get; }
        
        /// <summary>Description and business logic documentation</summary>
        public string Description { get; }
        
        /// <summary>Features that require lookback (for validation)</summary>
        public int MaxLookbackPeriods { get; }
        
        /// <summary>Whether features are computed in real-time or batch</summary>
        public bool IsRealTime { get; }

        public FeatureSetDefinition(
            string featureSetId,
            Version version,
            IReadOnlyList<string> featureNames,
            string checksum,
            string owner,
            DateTime createdAt,
            string description,
            int maxLookbackPeriods,
            bool isRealTime)
        {
            FeatureSetId = featureSetId;
            Version = version;
            FeatureNames = featureNames;
            Checksum = checksum;
            Owner = owner;
            CreatedAt = createdAt;
            Description = description;
            MaxLookbackPeriods = maxLookbackPeriods;
            IsRealTime = isRealTime;
        }

        /// <summary>
        /// Validates that feature vector matches this definition.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ValidateFeatureVector(double[] features)
        {
            return features != null && features.Length == FeatureNames.Count;
        }
    }

    /// <summary>
    /// Feature value with full lineage information.
    /// Used for both online serving and offline training.
    /// </summary>
    public readonly struct FeatureValue
    {
        /// <summary>Feature set this value belongs to</summary>
        public string FeatureSetId { get; }
        
        /// <summary>Instrument/symbol identifier</summary>
        public long InstrumentId { get; }
        
        /// <summary>Feature name</summary>
        public string FeatureName { get; }
        
        /// <summary>Feature value</summary>
        public double Value { get; }
        
        /// <summary>As-of timestamp (when this feature value is valid)</summary>
        public long AsOfTimestamp { get; }
        
        /// <summary>Computation timestamp (when this was computed)</summary>
        public long ComputedTimestamp { get; }
        
        /// <summary>Source of the data (e.g., "tick", "bar_1s", "orderbook")</summary>
        public string Source { get; }
        
        /// <summary>Version of feature computation logic</summary>
        public string ComputationVersion { get; }
    }

    /// <summary>
    /// Complete feature vector with full lineage.
    /// </summary>
    public readonly struct FeatureVector
    {
        /// <summary>Feature set definition used</summary>
        public FeatureSetDefinition Definition { get; }
        
        /// <summary>Instrument for this vector</summary>
        public long InstrumentId { get; }
        
        /// <summary>Feature values in same order as definition</summary>
        public double[] Values { get; }
        
        /// <summary>Timestamp these features are valid for</summary>
        public long AsOfTimestamp { get; }
        
        /// <summary>When these features were computed</summary>
        public long ComputedTimestamp { get; }
        
        /// <summary>Checksum for integrity verification</summary>
        public string Checksum { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid()
        {
            return Definition.ValidateFeatureVector(Values) && 
                   AsOfTimestamp > 0 && 
                   Checksum != null;
        }
    }

    /// <summary>
    /// Online feature retrieval request.
    /// </summary>
    public readonly struct FeatureRequest
    {
        /// <summary>Feature set to retrieve</summary>
        public string FeatureSetId { get; }
        
        /// <summary>Instrument to get features for</summary>
        public long InstrumentId { get; }
        
        /// <summary>As-of timestamp for time-travel semantics</summary>
        public long AsOfTimestamp { get; }
        
        /// <summary>Timeout for retrieval (microseconds)</summary>
        public int TimeoutMicroseconds { get; }
        
        /// <summary>Request ID for tracing</summary>
        public string RequestId { get; }

        public FeatureRequest(
            string featureSetId,
            long instrumentId,
            long asOfTimestamp,
            int timeoutMicroseconds,
            string requestId)
        {
            FeatureSetId = featureSetId;
            InstrumentId = instrumentId;
            AsOfTimestamp = asOfTimestamp;
            TimeoutMicroseconds = timeoutMicroseconds;
            RequestId = requestId;
        }
    }

    /// <summary>
    /// Feature store interface for both online and offline access.
    /// </summary>
    public interface IFeatureStore
    {
        /// <summary>Retrieve features for online inference</summary>
        FeatureVector GetFeatures(FeatureRequest request);
        
        /// <summary>Get historical feature values for training</summary>
        List<FeatureVector> GetHistoricalFeatures(
            string featureSetId,
            long instrumentId,
            DateTime startTime,
            DateTime endTime,
            TimeSpan interval);
        
        /// <summary>Register a new feature set definition</summary>
        void RegisterFeatureSet(FeatureSetDefinition definition);
        
        /// <summary>Store computed features (batch or real-time)</summary>
        void StoreFeatures(FeatureValue feature);
        
        /// <summary>Batch store features for efficiency</summary>
        void StoreFeaturesBatch(IEnumerable<FeatureValue> features);
        
        /// <summary>Get feature set definition by ID</summary>
        FeatureSetDefinition? GetFeatureSet(string featureSetId);
    }
}

