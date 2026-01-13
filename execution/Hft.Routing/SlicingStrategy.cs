using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Hft.Core;
using Hft.OrderBook;

namespace Hft.Routing
{
    /// <summary>
    /// Slicing strategy for distributing parent orders into child orders.
    /// 
    /// Supported Strategies:
    /// - POV (Percentage of Volume): Execute at fixed % of market volume
    /// - TWAP (Time Weighted Average Price): Even distribution over time
    /// - VWAP (Volume Weighted Average Price): Target VWAP curve
    /// - IS (Implementation Shortfall): Optimize for total cost
    /// 
    /// Design:
    /// - Dynamic rebalancing based on market conditions
    /// - Adaptive slice sizing based on liquidity
    /// - Emergency fallback handling
    /// 
    /// Performance: Target <10μs per slice computation
    /// </summary>
    public interface ISlicingStrategy
    {
        /// <summary>
        /// Gets the strategy type.
        /// </summary>
        ExecutionStrategy StrategyType { get; }

        /// <summary>
        /// Computes the next slice for a parent order.
        /// </summary>
        /// <param name="parentOrder">Parent order</param>
        /// <param name="remainingQuantity">Remaining quantity to execute</param>
        /// <param name="marketData">Current market data</param>
        /// <param name="elapsedTime">Elapsed time since start (microseconds)</param>
        /// <param name="totalTime">Total allocated time (microseconds)</param>
        /// <returns>Slice specification</returns>
        SliceSpec ComputeSlice(
            ParentOrder parentOrder,
            int remainingQuantity,
            MarketDataSnapshot marketData,
            long elapsedTime,
            long totalTime);

        /// <summary>
        /// Updates the strategy with execution feedback.
        /// </summary>
        void UpdateFeedback(SliceFeedback feedback);

        /// <summary>
        /// Resets the strategy state.
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// Slice specification for a single child order.
    /// </summary>
    public readonly struct SliceSpec
    {
        /// <summary>Quantity for this slice</summary>
        public int Quantity { get; init; }

        /// <summary>Order type for this slice</summary>
        public OrderType OrderType { get; init; }

        /// <summary>Time-in-force</summary>
        public TimeInForce TimeInForce { get; init; }

        /// <summary>Limit price (if applicable)</summary>
        public double? LimitPrice { get; init; }

        /// <summary>Order flags</summary>
        public OrderAttributes Flags { get; init; }

        /// <summary>Slice urgency (0-1)</summary>
        public double Urgency { get; init; }

        /// <summary>Reason for slice size</summary>
        public string Reason { get; init; }

        /// <summary>
        /// Creates a slice specification.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SliceSpec Create(
            int quantity,
            OrderType orderType,
            TimeInForce tif,
            double? limitPrice,
            OrderAttributes flags,
            double urgency,
            string reason)
        {
            return new SliceSpec
            {
                Quantity = quantity,
                OrderType = orderType,
                TimeInForce = tif,
                LimitPrice = limitPrice,
                Flags = flags,
                Urgency = urgency,
                Reason = reason
            };
        }
    }

    /// <summary>
    /// Feedback from slice execution.
    /// </summary>
    public readonly struct SliceFeedback
    {
        /// <summary>Quantity sent in slice</summary>
        public int SentQuantity { get; init; }

        /// <summary>Quantity filled</summary>
        public int FilledQuantity { get; init; }

        /// <summary>Average fill price</summary>
        public double AvgFillPrice { get; init; }

        /// <summary>Time to fill (microseconds)</summary>
        public long FillTimeUs { get; init; }

        /// <summary>Implementation shortfall (bps)</summary>
        public double ShortfallBps { get; init; }

        /// <summary>
        /// Creates slice feedback.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SliceFeedback Create(
            int sent,
            int filled,
            double avgPrice,
            long fillTime,
            double shortfall)
        {
            return new SliceFeedback
            {
                SentQuantity = sent,
                FilledQuantity = filled,
                AvgFillPrice = avgPrice,
                FillTimeUs = fillTime,
                ShortfallBps = shortfall
            };
        }
    }

    /// <summary>
    /// Market data snapshot for slicing decisions.
    /// </summary>
    public readonly struct MarketDataSnapshot
    {
        /// <summary>Instrument ID</summary>
        public long InstrumentId { get; init; }

        /// <summary>Timestamp</summary>
        public long Timestamp { get; init; }

        /// <summary>Best bid price</summary>
        public long BestBidPrice { get; init; }

        /// <summary>Best ask price</summary>
        public long BestAskPrice { get; init; }

        /// <summary>Mid price</summary>
        public double MidPrice => (BestBidPrice + BestAskPrice) / 2.0;

        /// <summary>Spread (bps)</summary>
        public double SpreadBps => BestAskPrice > BestBidPrice 
            ? ((BestAskPrice - BestBidPrice) / MidPrice) * 10000 : 0;

        /// <summary>Estimated volume in past interval</summary>
        public long VolumeInterval { get; init; }

        /// <summary>ADV (Average Daily Volume)</summary>
        public long AverageDailyVolume { get; init; }

        /// <summary>Recent trade count</summary>
        public int RecentTradeCount { get; init; }

        /// <summary>Recent volume</summary>
        public long RecentVolume { get; init; }

        /// <summary>Venue BBO per venue</summary>
        public IReadOnlyList<VenueBboSnapshot> VenueBbos { get; init; }

        /// <summary>
        /// Creates a market data snapshot.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MarketDataSnapshot Create(
            long instrumentId,
            long timestamp,
            long bidPrice,
            long askPrice,
            long volumeInterval,
            long adv,
            int tradeCount,
            long recentVolume,
            IReadOnlyList<VenueBboSnapshot> bbos)
        {
            return new MarketDataSnapshot
            {
                InstrumentId = instrumentId,
                Timestamp = timestamp,
                BestBidPrice = bidPrice,
                BestAskPrice = askPrice,
                VolumeInterval = volumeInterval,
                AverageDailyVolume = adv,
                RecentTradeCount = tradeCount,
                RecentVolume = recentVolume,
                VenueBbos = bbos
            };
        }
    }

    /// <summary>
    /// POV (Percentage of Volume) slicing strategy.
    /// 
    /// Algorithm:
    ///   sliceSize = min(maxSlice, minSlice, povPercentage × volumeInterval)
    ///   
    ///   Where:
    ///   - povPercentage: Configured participation rate (e.g., 10% = 0.10)
    ///   - volumeInterval: Estimated volume since last slice
    ///   - maxSlice: Maximum slice size (liquidity constraint)
    ///   - minSlice: Minimum slice size
    /// 
    /// Dynamic Rebalancing:
    ///   If actual participation > target, reduce slice size
    ///   If actual participation < target, increase slice size
    /// 
    /// Formula:
    ///   participationRatio = actualFilled / expectedVolume
    ///   adjustmentFactor = min(1.5, max(0.5, targetParticipation / participationRatio))
    ///   sliceSize = baseSlice × adjustmentFactor
    /// 
    /// Rationale:
    /// - Fixed participation avoids market impact
    /// - Dynamic adjustment responds to actual conditions
    /// - Minimum slice ensures progress
    /// - Maximum slice prevents over-execution
    /// </summary>
    public sealed class PovSlicingStrategy : ISlicingStrategy
    {
        private readonly double _povPercentage;
        private readonly int _minSliceSize;
        private readonly int _maxSliceSize;
        private readonly double _adjustmentFactor;
        
        // Adaptive state
        private double _currentAdjustment = 1.0;
        private long _previousVolume;
        private long _previousTimestamp;

        public ExecutionStrategy StrategyType => ExecutionStrategy.POV;

        public PovSlicingStrategy(
            double povPercentage = 0.10,
            int minSliceSize = 100,
            int maxSliceSize = 10000,
            double adjustmentFactor = 1.0)
        {
            _povPercentage = povPercentage;
            _minSliceSize = minSliceSize;
            _maxSliceSize = maxSliceSize;
            _adjustmentFactor = adjustmentFactor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SliceSpec ComputeSlice(
            ParentOrder parentOrder,
            int remainingQuantity,
            MarketDataSnapshot marketData,
            long elapsedTime,
            long totalTime)
        {
            // Calculate volume since last slice
            long volumeInterval = marketData.RecentVolume > 0 
                ? marketData.RecentVolume 
                : Math.Max(1, marketData.VolumeInterval);

            // Base slice size from POV
            double baseSlice = volumeInterval * _povPercentage * _currentAdjustment;

            // Apply min/max constraints
            int sliceSize = (int)Math.Round(baseSlice);
            sliceSize = Math.Max(_minSliceSize, Math.Min(_maxSliceSize, sliceSize));

            // Don't exceed remaining quantity
            sliceSize = Math.Min(sliceSize, remainingQuantity);

            // Calculate urgency based on time progress
            double timeProgress = (double)elapsedTime / totalTime;
            double urgency = Math.Min(1.0, timeProgress * 1.2); // Slightly accelerate

            // Use market orders for high urgency
            OrderType orderType = urgency > 0.7 ? OrderType.Market : OrderType.Limit;

            // Determine if we should use passive or aggressive
            OrderAttributes flags = OrderAttributes.None;
            if (parentOrder.Intent == OrderIntent.Passive && orderType == OrderType.Limit)
            {
                flags |= OrderAttributes.PostOnly;
            }

            string reason = $"POV: {(_povPercentage * 100):F1}% × vol={volumeInterval} adj={_currentAdjustment:F2}";

            return SliceSpec.Create(
                quantity: sliceSize,
                orderType: orderType,
                tif: TimeInForce.Day,
                limitPrice: parentOrder.LimitPrice,
                flags: flags,
                urgency: urgency,
                reason: reason);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateFeedback(SliceFeedback feedback)
        {
            // Calculate actual participation
            if (feedback.FillTimeUs > 0)
            {
                // Adjust based on fill rate
                double fillRate = (double)feedback.FilledQuantity / feedback.SentQuantity;
                
                // If fill rate is high, we can be more passive
                // If fill rate is low, we need to be more aggressive
                if (fillRate < 0.5)
                {
                    _currentAdjustment = Math.Min(2.0, _currentAdjustment * 1.2);
                }
                else if (fillRate > 0.9)
                {
                    _currentAdjustment = Math.Max(0.5, _currentAdjustment * 0.9);
                }
            }
        }

        public void Reset()
        {
            _currentAdjustment = 1.0;
            _previousVolume = 0;
            _previousTimestamp = 0;
        }
    }

    /// <summary>
    /// TWAP (Time Weighted Average Price) slicing strategy.
    /// 
    /// Algorithm:
    ///   sliceSize = remainingQuantity / remainingIntervals
    ///   
    ///   Where:
    ///   - remainingIntervals = ceil(remainingTime / intervalDuration)
    ///   - intervalDuration = totalTime / configuredIntervals
    /// 
    /// Dynamic Adjustment:
    ///   If behind schedule, increase slice size
    ///   If ahead of schedule, decrease slice size
    /// 
    /// Formula:
    ///   scheduleProgress = elapsedTime / totalTime
    ///   quantityProgress = filledQuantity / totalQuantity
    ///   adjustmentRatio = scheduleProgress / max(0.01, quantityProgress)
    ///   sliceSize = baseSlice × adjustmentRatio
    /// 
    /// Rationale:
    /// - Even time distribution minimizes time-related bias
    /// - Dynamic adjustment responds to execution pace
    /// - Simple and predictable execution pattern
    /// </summary>
    public sealed class TwapSlicingStrategy : ISlicingStrategy
    {
        private readonly int _intervalCount;
        private readonly double _minParticipation;
        private readonly double _maxParticipation;
        
        private int _currentInterval;
        private long _startTimestamp;

        public ExecutionStrategy StrategyType => ExecutionStrategy.TWAP;

        public TwapSlicingStrategy(
            int intervalCount = 20,
            double minParticipation = 0.5,
            double maxParticipation = 2.0)
        {
            _intervalCount = intervalCount;
            _minParticipation = minParticipation;
            _maxParticipation = maxParticipation;
            _currentInterval = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SliceSpec ComputeSlice(
            ParentOrder parentOrder,
            int remainingQuantity,
            MarketDataSnapshot marketData,
            long elapsedTime,
            long totalTime)
        {
            // Calculate remaining intervals
            int remainingIntervals = Math.Max(1, _intervalCount - _currentInterval);
            
            // Base slice: equal distribution
            int baseSlice = remainingQuantity / remainingIntervals;

            // Calculate schedule progress
            double timeProgress = totalTime > 0 ? (double)elapsedTime / totalTime : 0;
            double expectedProgress = (_currentInterval + 1.0) / _intervalCount;
            
            // Adjust for being behind/ahead of schedule
            double adjustment = 1.0;
            if (timeProgress > expectedProgress * 1.1)
            {
                // Behind schedule - increase slice
                adjustment = 1.0 + (timeProgress - expectedProgress) * 2;
            }
            else if (timeProgress < expectedProgress * 0.9)
            {
                // Ahead of schedule - can reduce slice
                adjustment = Math.Max(_minParticipation, 1.0 - (expectedProgress - timeProgress));
            }

            adjustment = Math.Max(_minParticipation, Math.Min(_maxParticipation, adjustment));

            int sliceSize = (int)(baseSlice * adjustment);
            sliceSize = Math.Max(parentOrder.StrategyParameters.MinSliceSize, sliceSize);
            sliceSize = Math.Min(sliceSize, remainingQuantity);

            // Calculate urgency based on remaining time
            double remainingTimeRatio = 1.0 - (double)elapsedTime / totalTime;
            double urgency = Math.Min(1.0, 1.0 - remainingTimeRatio * 0.5);

            OrderType orderType = urgency > 0.8 ? OrderType.Market : OrderType.Limit;

            OrderAttributes flags = OrderAttributes.None;
            if (parentOrder.Intent == OrderIntent.Passive && orderType == OrderType.Limit)
            {
                flags |= OrderAttributes.PostOnly;
            }

            string reason = $"TWAP: interval {_currentInterval + 1}/{_intervalCount} adj={adjustment:F2}";

            _currentInterval++;

            return SliceSpec.Create(
                quantity: sliceSize,
                orderType: orderType,
                tif: TimeInForce.Day,
                limitPrice: parentOrder.LimitPrice,
                flags: flags,
                urgency: urgency,
                reason: reason);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateFeedback(SliceFeedback feedback)
        {
            // TWAP doesn't adjust based on feedback - fixed schedule
        }

        public void Reset()
        {
            _currentInterval = 0;
            _startTimestamp = 0;
        }
    }

    /// <summary>
    /// VWAP (Volume Weighted Average Price) slicing strategy.
    /// 
    /// Algorithm:
    ///   targetSlice = expectedVolumeAtInterval × targetParticipation
    ///   expectedVolumeAtInterval = ADV × (intervalDuration / tradingDay)
    /// 
    /// Volume Profile:
    ///   Uses intraday volume profile to weight execution
    ///   Early day: lower participation (less volume)
    ///   Mid day: higher participation (most volume)
    ///   Late day: lower participation (less volume)
    /// 
    /// Formula:
    ///   volumeProfileWeight = intradayVolumeArray[currentInterval] / max(intradayVolumeArray)
    ///   targetSlice = baseSlice × volumeProfileWeight × participationRate
    /// 
    /// Rationale:
    /// - Matches execution to natural volume flow
    /// - Minimizes impact by executing when others are trading
    /// - Volume profile captures intraday patterns
    /// </summary>
    public sealed class VwapSlicingStrategy : ISlicingStrategy
    {
        private readonly double _participationRate;
        private readonly double[] _volumeProfile;
        private readonly int _intervalCount;
        
        private int _currentInterval;
        private double _totalVolumeProfile;

        public ExecutionStrategy StrategyType => ExecutionStrategy.VWAP;

        /// <summary>
        /// Creates a VWAP strategy with default intraday volume profile.
        /// </summary>
        /// <param name="participationRate">Target participation rate (e.g., 0.15 = 15%)</param>
        /// <param name="intervalCount">Number of intervals</param>
        /// <param name="volumeProfile">Intraday volume weights (normalized, sum = 1)</param>
        public VwapSlicingStrategy(
            double participationRate = 0.15,
            int intervalCount = 20,
            double[]? volumeProfile = null)
        {
            _participationRate = participationRate;
            _intervalCount = intervalCount;
            _currentInterval = 0;

            // Default US equity volume profile (hourly buckets)
            // Pattern: open surge, mid-day lull, close surge
            _volumeProfile = volumeProfile ?? new double[]
            {
                0.08, // 9:30-10:30
                0.06, // 10:30-11:30
                0.05, // 11:30-12:30
                0.05, // 12:30-13:30
                0.05, // 13:30-14:30
                0.06, // 14:30-15:30
                0.10, // 15:30-16:00 (close surge)
                0.05  // Extended hours
            };

            // Normalize and calculate total
            double sum = 0;
            foreach (var w in _volumeProfile)
                sum += w;
            _totalVolumeProfile = sum;

            // Adjust interval count to match profile
            if (_volumeProfile.Length != _intervalCount)
            {
                // Interpolate or truncate to match interval count
                var adjusted = new double[_intervalCount];
                double ratio = (double)_volumeProfile.Length / _intervalCount;
                for (int i = 0; i < _intervalCount; i++)
                {
                    int srcIdx1 = (int)(i * ratio);
                    int srcIdx2 = Math.Min(srcIdx1 + 1, _volumeProfile.Length - 1);
                    double weight = (i * ratio) - srcIdx1;
                    adjusted[i] = _volumeProfile[srcIdx1] * (1 - weight) + _volumeProfile[srcIdx2] * weight;
                }
                _volumeProfile = adjusted;
                _totalVolumeProfile = 0;
                foreach (var w in _volumeProfile)
                    _totalVolumeProfile += w;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SliceSpec ComputeSlice(
            ParentOrder parentOrder,
            int remainingQuantity,
            MarketDataSnapshot marketData,
            long elapsedTime,
            long totalTime)
        {
            // Get volume profile weight for current interval
            double volumeWeight = _currentInterval < _volumeProfile.Length
                ? _volumeProfile[_currentInterval] / _totalVolumeProfile
                : 1.0 / _intervalCount;

            // Calculate expected volume for this interval
            long expectedVolume = (long)(marketData.AverageDailyVolume * volumeWeight);

            // Target slice based on participation rate and expected volume
            int targetSlice = (int)(expectedVolume * _participationRate);

            // Adjust for remaining quantity
            double remainingRatio = (double)remainingQuantity / parentOrder.TotalQuantity;
            int adjustedSlice = (int)(targetSlice * Math.Sqrt(remainingRatio));

            // Apply min/max constraints
            adjustedSlice = Math.Max(
                parentOrder.StrategyParameters.MinSliceSize,
                Math.Min(parentOrder.StrategyParameters.MaxSliceSize > 0 
                    ? parentOrder.StrategyParameters.MaxSliceSize 
                    : remainingQuantity, adjustedSlice));

            adjustedSlice = Math.Min(adjustedSlice, remainingQuantity);

            // Calculate urgency based on volume-weighted time
            double timeProgress = totalTime > 0 ? (double)elapsedTime / totalTime : 0;
            double volumeProgress = 0;
            for (int i = 0; i <= _currentInterval; i++)
            {
                volumeProgress += _volumeProfile[i];
            }
            volumeProgress /= _totalVolumeProfile;

            double urgency = Math.Min(1.0, timeProgress / Math.Max(0.01, volumeProgress));

            OrderType orderType = urgency > 0.85 ? OrderType.Market : OrderType.Limit;

            OrderAttributes flags = OrderAttributes.None;
            if (parentOrder.Intent == OrderIntent.Passive && orderType == OrderType.Limit)
            {
                flags |= OrderAttributes.PostOnly;
            }

            string reason = $"VWAP: volWeight={volumeWeight:F3} target={targetSlice} adj={adjustedSlice}";

            _currentInterval++;

            return SliceSpec.Create(
                quantity: adjustedSlice,
                orderType: orderType,
                tif: TimeInForce.Day,
                limitPrice: parentOrder.LimitPrice,
                flags: flags,
                urgency: urgency,
                reason: reason);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateFeedback(SliceFeedback feedback)
        {
            // Could adjust participation rate based on feedback
            // For now, maintain static profile
        }

        public void Reset()
        {
            _currentInterval = 0;
        }
    }

    /// <summary>
    /// Almgren-Chriss Optimal Execution strategy.
    /// 
    /// Minimized Cost = Permanent Impact + Temporary Impact + Volatility Risk.
    /// 
    /// Optimal Trajectory:
    ///   x_k = X * sinh(kappa * (T - t_k)) / sinh(kappa * T)
    /// 
    /// Where:
    ///   kappa = sqrt(lambda * sigma^2 / eta)
    ///   lambda: Risk aversion
    ///   sigma: Volatility
    ///   eta: Temporary impact
    /// </summary>
    public sealed class AlmgrenChrissSlicingStrategy : ISlicingStrategy
    {
        private readonly double _kappa;
        private readonly double _totalQuantity;
        private readonly long _totalTime;
        private int _lastScheduledQuantity;

        public ExecutionStrategy StrategyType => ExecutionStrategy.AlmgrenChriss;

        public AlmgrenChrissSlicingStrategy(ParentOrder parentOrder, long totalTime)
        {
            var p = parentOrder.StrategyParameters;
            double sigma = p.DailyVolatility;
            double lambda = p.RiskAversion;
            double eta = p.TemporaryImpact;

            // kappa = sqrt(lambda * sigma^2 / eta)
            _kappa = Math.Sqrt((lambda * sigma * sigma) / Math.Max(1e-9, eta));
            _totalQuantity = parentOrder.TotalQuantity;
            _totalTime = Math.Max(1, totalTime);
            _lastScheduledQuantity = (int)_totalQuantity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SliceSpec ComputeSlice(
            ParentOrder parentOrder,
            int remainingQuantity,
            MarketDataSnapshot marketData,
            long elapsedTime,
            long totalTime)
        {
            double t_k = elapsedTime;
            double T = _totalTime;

            // Calculate x_k: remaining quantity according to optimal trajectory
            double sinhKappaT = Math.Sinh(_kappa * T);
            double sinhKappaTMinusTk = Math.Sinh(_kappa * Math.Max(0, T - t_k));

            double x_k = sinhKappaT > 0 
                ? _totalQuantity * (sinhKappaTMinusTk / sinhKappaT)
                : _totalQuantity * (1.0 - (t_k / T)); // Fallback to linear (TWAP) if kappa is 0

            int targetRemaining = (int)Math.Round(x_k);
            int sliceSize = _lastScheduledQuantity - targetRemaining;

            // Ensure slice is positive and respects constraints
            sliceSize = Math.Max(parentOrder.StrategyParameters.MinSliceSize, sliceSize);
            sliceSize = Math.Min(sliceSize, remainingQuantity);

            _lastScheduledQuantity = targetRemaining;

            // Dynamic urgency based on drift from trajectory
            double drift = (remainingQuantity - x_k) / _totalQuantity;
            double urgency = Math.Min(1.0, 0.5 + drift * 5.0); // Increase urgency if behind

            OrderType orderType = urgency > 0.8 ? OrderType.Market : OrderType.Limit;

            string reason = $"AC: kappa={_kappa:F6} target_rem={targetRemaining} drift={drift:F4}";

            return SliceSpec.Create(
                quantity: sliceSize,
                orderType: orderType,
                tif: TimeInForce.Day,
                limitPrice: parentOrder.LimitPrice,
                flags: OrderAttributes.None,
                urgency: urgency,
                reason: reason);
        }

        public void UpdateFeedback(SliceFeedback feedback) { }

        public void Reset() { }
    }

    /// <summary>
    /// Factory for creating slicing strategies.
    /// </summary>
    public static class SlicingStrategyFactory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ISlicingStrategy Create(ParentOrder parentOrder, long totalTimeUs)
        {
            var strategy = parentOrder.Strategy;
            var params_ = parentOrder.StrategyParameters;

            return strategy switch
            {
                ExecutionStrategy.POV => new PovSlicingStrategy(
                    povPercentage: params_.PovPercentage,
                    minSliceSize: params_.MinSliceSize,
                    maxSliceSize: params_.MaxSliceSize > 0 ? params_.MaxSliceSize : 10000),

                ExecutionStrategy.TWAP => new TwapSlicingStrategy(
                    intervalCount: params_.TwapIntervalCount,
                    minParticipation: 0.5,
                    maxParticipation: 2.0),

                ExecutionStrategy.VWAP => new VwapSlicingStrategy(
                    participationRate: params_.VwapParticipationLimit,
                    intervalCount: params_.TwapIntervalCount),

                ExecutionStrategy.AlmgrenChriss => new AlmgrenChrissSlicingStrategy(
                    parentOrder, totalTimeUs),

                _ => new PovSlicingStrategy()
            };
        }
    }
}

