using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Hft.Core;
using Hft.OrderBook;

namespace Hft.Routing
{
    /// <summary>
    /// Mock venue adapter for testing and simulation.
    /// Provides deterministic behavior with configurable latency.
    /// 
    /// Features:
    /// - Configurable latency distribution
    /// - Deterministic fill simulation based on queue position
    /// - Fee schedule configuration
    /// - Rate limiting simulation
    /// 
    /// Usage:
    /// var venue = new MockVenueAdapter("NASDAQ", config);
    /// venue.SetupOrderBook(liquidity);
    /// </summary>
    public sealed class MockVenueAdapter : IVenueAdapter
    {
        private readonly string _venueId;
        private readonly string _venueName;
        private readonly VenueType _venueType;
        private readonly MockVenueConfig _config;
        
        // Order book state
        private Dictionary<long, MockOrderState> _orders;
        private List<VenuePriceLevel> _bidLevels;
        private List<VenuePriceLevel> _askLevels;
        
        // Performance metrics
        private long _orderCount;
        private long _fillCount;
        private double _totalLatencyUs;
        private List<double> _latencySamples;
        
        // Rate limiting
        private long _lastRateReset;
        private int _ordersThisSecond;
        
        // Thread safety
        private long _disposed;

        public string VenueId => _venueId;
        public string VenueName => _venueName;
        public VenueType VenueType => _venueType;
        public bool IsAvailable => !_config.DisableVenue;

        public MockVenueAdapter(
            string venueId,
            string? venueName = null,
            VenueType venueType = VenueType.Exchange,
            MockVenueConfig? config = null)
        {
            _venueId = venueId;
            _venueName = venueName ?? venueId;
            _venueType = venueType;
            _config = config ?? new MockVenueConfig();
            
            _orders = new Dictionary<long, MockOrderState>();
            _bidLevels = new List<VenuePriceLevel>();
            _askLevels = new List<VenuePriceLevel>();
            _latencySamples = new List<double>(1000);
            _orderCount = 0;
            _fillCount = 0;
            _totalLatencyUs = 0;
            _lastRateReset = Stopwatch.GetTimestamp();
            
            // Initialize with default liquidity if configured
            if (_config.InitialLiquidity != null)
            {
                SetupOrderBook(_config.InitialLiquidity);
            }
        }

        /// <summary>
        /// Sets up the order book with initial liquidity.
        /// </summary>
        public void SetupOrderBook(MockLiquiditySetup liquidity)
        {
            _bidLevels.Clear();
            _askLevels.Clear();

            // Setup bid levels
            long baseBid = liquidity.MidPrice - liquidity.Spread / 2;
            for (int i = 0; i < liquidity.BidLevels; i++)
            {
                long price = baseBid - i * liquidity.TickSize;
                int size = liquidity.BaseSize + i * 100;
                _bidLevels.Add(new VenuePriceLevel
                {
                    Price = price,
                    Size = size,
                    OrderCount = size / 100,
                    IsHidden = false
                });
            }

            // Setup ask levels
            long baseAsk = baseBid + liquidity.Spread;
            for (int i = 0; i < liquidity.AskLevels; i++)
            {
                long price = baseAsk + i * liquidity.TickSize;
                int size = liquidity.BaseSize + i * 100;
                _askLevels.Add(new VenuePriceLevel
                {
                    Price = price,
                    Size = size,
                    OrderCount = size / 100,
                    IsHidden = false
                });
            }
        }

        public VenueStatus GetStatus()
        {
            return IsAvailable ? VenueStatus.Trading : VenueStatus.Unavailable;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OrderAck SendOrder(OrderQueueEntry order)
        {
            var startTime = Stopwatch.GetTimestamp();
            var latencyUs = 0.0;

            try
            {
                // Apply latency
                if (_config.LatencyMedianUs > 0)
                {
                    latencyUs = SampleLatency();
                    var spinWait = new SpinWait();
                    spinWait.SpinOnce(); // Simulate latency
                }

                // Check rate limit
                if (!CheckRateLimit())
                {
                    return OrderAck.Rejected(order.OrderId, "Rate limited", latencyUs);
                }

                // Validate order
                var validation = ValidateOrder(order);
                if (!validation.IsValid)
                {
                    return OrderAck.Rejected(order.OrderId, validation.RejectReason ?? "Validation failed", latencyUs);
                }

                // Create order state
                var orderState = new MockOrderState
                {
                    Order = order,
                    Status = OrderStatus.Active,
                    FilledQuantity = 0,
                    AckTimestamp = Stopwatch.GetTimestamp()
                };

                // Add to order book
                lock (_orders)
                {
                    _orders[order.OrderId] = orderState;
                }

                // Simulate fill based on order type and queue position
                if (_config.InstantFillProbability > 0 && order.Type != OrderType.Limit)
                {
                    double fillRoll = new Random((int)(order.OrderId ^ _config.RandomSeed)).NextDouble();
                    if (fillRoll < _config.InstantFillProbability)
                    {
                        // Instant fill
                        orderState.Status = OrderStatus.Filled;
                        orderState.FilledQuantity = order.LeavesQuantity;
                        Interlocked.Increment(ref _fillCount);
                    }
                }

                Interlocked.Increment(ref _orderCount);
                _totalLatencyUs += latencyUs;
                _latencySamples.Add(latencyUs);

                int? queuePos = order.Type == OrderType.Limit 
                    ? EstimateQueuePosition(order) 
                    : null;

                return OrderAck.Accepted(
                    localOrderId: order.OrderId,
                    exchangeOrderId: $"{_venueId}-{order.OrderId}",
                    queuePosition: queuePos,
                    latencyUs: latencyUs);
            }
            catch (Exception ex)
            {
                return OrderAck.Rejected(order.OrderId, $"Exception: {ex.Message}", latencyUs);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CancelOrder(long orderId)
        {
            lock (_orders)
            {
                if (_orders.TryGetValue(orderId, out var orderState))
                {
                    if (orderState.Status == OrderStatus.Active)
                    {
                        orderState.Status = OrderStatus.Canceled;
                        _orders.Remove(orderId);
                        return true;
                    }
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AmendOrder(long orderId, int newQuantity)
        {
            lock (_orders)
            {
                if (_orders.TryGetValue(orderId, out var orderState))
                {
                    if (orderState.Status == OrderStatus.Active)
                    {
                        var newOrder = orderState.Order.WithLeavesQuantity(newQuantity);
                        orderState.Order = newOrder;
                        return true;
                    }
                }
            }
            return false;
        }

        public OrderStatus? GetOrderStatus(long orderId)
        {
            lock (_orders)
            {
                if (_orders.TryGetValue(orderId, out var state))
                {
                    return state.Status;
                }
            }
            return null;
        }

        public VenueOrderBookSnapshot? GetOrderBook(long instrumentId)
        {
            lock (_bidLevels)
            {
                return new VenueOrderBookSnapshot
                {
                    InstrumentId = instrumentId,
                    Timestamp = Stopwatch.GetTimestamp(),
                    Bids = _bidLevels.ToList(),
                    Asks = _askLevels.ToList()
                };
            }
        }

        public VenueBboSnapshot? GetBestBidAsk(long instrumentId)
        {
            var bestBidLevel = _bidLevels.FirstOrDefault();
            var bestAskLevel = _askLevels.FirstOrDefault();

            if (bestBidLevel.Price == 0 || bestAskLevel.Price == 0)
                return null;

            return new VenueBboSnapshot
            {
                InstrumentId = instrumentId,
                Timestamp = Stopwatch.GetTimestamp(),
                BestBidPrice = bestBidLevel.Price,
                BestBidSize = bestBidLevel.Size,
                BestAskPrice = bestAskLevel.Price,
                BestAskSize = bestAskLevel.Size
            };
        }

        public double EstimateFillProbability(OrderSide side, long price, int quantity, OrderType orderType)
        {
            if (orderType != OrderType.Limit)
                return _config.InstantFillProbability;

            // Estimate based on queue position
            int queuePos = EstimateQueuePosition(side, price);
            double baseProb = Math.Exp(-0.5 * queuePos); // Decay factor
            
            // Adjust for quantity
            double sizeFactor = Math.Exp(-(double)quantity / 10000);
            
            return Math.Max(0.01, baseProb * sizeFactor);
        }

        private int EstimateQueuePosition(OrderQueueEntry order)
        {
            return EstimateQueuePosition(order.Side, order.Price);
        }

        private int EstimateQueuePosition(OrderSide side, long price)
        {
            int position = 1;
            
            if (side == OrderSide.Buy)
            {
                // Find position in asks
                foreach (var level in _askLevels)
                {
                    if (level.Price <= price)
                        break;
                    position += level.OrderCount;
                }
            }
            else
            {
                // Find position in bids
                foreach (var level in _bidLevels)
                {
                    if (level.Price >= price)
                        break;
                    position += level.OrderCount;
                }
            }
            
            return position;
        }

        public double GetLatencyMicroseconds()
        {
            if (_latencySamples.Count == 0)
                return _config.LatencyMedianUs;

            lock (_latencySamples)
            {
                return _totalLatencyUs / _latencySamples.Count;
            }
        }

        public VenueFeeSchedule GetFeeSchedule(long instrumentId)
        {
            return _config.FeeSchedule ?? new VenueFeeSchedule
            {
                InstrumentId = instrumentId,
                MakerFeeBps = -0.2, // Rebate
                TakerFeeBps = 0.3,
                MinimumFee = 0.01,
                LiquidityRebateBps = 0.2,
                RegulatoryFeeBps = 0.1,
                PassThroughRebates = true
            };
        }

        public VenueOrderValidation ValidateOrder(OrderQueueEntry order)
        {
            // Basic validation
            if (order.OrderId <= 0)
                return VenueOrderValidation.Invalid("Invalid order ID");

            if (order.LeavesQuantity <= 0)
                return VenueOrderValidation.Invalid("Invalid quantity");

            if (order.Price < 0)
                return VenueOrderValidation.Invalid("Invalid price");

            // Check quantity limits
            if (_config.MaxOrderSize > 0 && order.LeavesQuantity > _config.MaxOrderSize)
            {
                return VenueOrderValidation.Invalid($"Quantity exceeds max ({_config.MaxOrderSize})");
            }

            // Check price limits
            if (_config.PriceBandPercent > 0)
            {
                var bbo = GetBestBidAsk(order.InstrumentId);
                if (bbo != null)
                {
                    double midPrice = bbo.Value.MidPrice;
                    double maxDeviation = midPrice * _config.PriceBandPercent / 100;
                    if (Math.Abs(order.Price - midPrice) > maxDeviation)
                    {
                        return VenueOrderValidation.Invalid("Price outside band");
                    }
                }
            }

            return VenueOrderValidation.Valid();
        }

        public RateLimitStatus GetRateLimitStatus()
        {
            long now = Stopwatch.GetTimestamp();
            long elapsed = now - _lastRateReset;
            
            if (elapsed >= Stopwatch.Frequency) // 1 second
            {
                Interlocked.Exchange(ref _lastRateReset, now);
                Interlocked.Exchange(ref _ordersThisSecond, 0);
            }

            int orders = Interlocked.CompareExchange(ref _ordersThisSecond, 0, 0);
            int remaining = Math.Max(0, _config.MaxOrdersPerSecond - orders);

            return new RateLimitStatus
            {
                OrdersPerSecond = orders,
                MaxOrdersPerSecond = _config.MaxOrdersPerSecond,
                OrdersRemaining = remaining,
                MessagesPerSecond = orders * 2,
                MaxMessagesPerSecond = _config.MaxOrdersPerSecond * 10,
                NotionalPerSecond = orders * 100000,
                MaxNotionalPerSecond = _config.MaxOrdersPerSecond * 500000,
                IsRateLimited = remaining <= 0,
                ResetInMicroseconds = Math.Max(0, (long)(Stopwatch.Frequency - elapsed) * 1_000_000 / Stopwatch.Frequency)
            };
        }

        public VenuePerformanceMetrics GetPerformanceMetrics()
        {
            double avgLatency = _latencySamples.Count > 0 
                ? _totalLatencyUs / _latencySamples.Count 
                : 0;

            var sortedLatencies = _latencySamples.OrderBy(x => x).ToList();
            double p50 = sortedLatencies.Count > 0 
                ? sortedLatencies[sortedLatencies.Count / 2] 
                : 0;
            double p99 = sortedLatencies.Count > 0 
                ? sortedLatencies[(int)(sortedLatencies.Count * 0.99)] 
                : 0;

            return new VenuePerformanceMetrics
            {
                VenueId = _venueId,
                PeriodStart = 0,
                PeriodEnd = Stopwatch.GetTimestamp(),
                TotalOrdersSent = _orderCount,
                TotalOrdersFilled = _fillCount,
                TotalQuantityFilled = _fillCount * 100, // Estimate
                AvgLatencyUs = avgLatency,
                P50LatencyUs = p50,
                P99LatencyUs = p99,
                AvgImplementationShortfallBps = _config.DefaultShortfallBps,
                AvgEffectiveSpreadBps = _config.DefaultSpreadBps,
                RejectionRate = 0.01,
                CancelRate = 0.05
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double SampleLatency()
        {
            // Log-normal distribution
            double mu = Math.Log(_config.LatencyMedianUs);
            double sigma = _config.LatencyStdDev * _config.LatencyMedianUs;
            
            double u1 = 1.0 - new Random((int)(Stopwatch.GetTimestamp() ^ _config.RandomSeed)).NextDouble();
            double u2 = 1.0 - new Random().NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            
            return Math.Exp(mu + sigma * z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CheckRateLimit()
        {
            int current = Interlocked.Increment(ref _ordersThisSecond);
            return current <= _config.MaxOrdersPerSecond;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _orders.Clear();
                _latencySamples.Clear();
            }
        }
    }

    /// <summary>
    /// Mock order state.
    /// </summary>
    internal class MockOrderState
    {
        public OrderQueueEntry Order { get; set; }
        public OrderStatus Status { get; set; }
        public int FilledQuantity { get; set; }
        public long AckTimestamp { get; set; }
    }

    /// <summary>
    /// Configuration for mock venue adapter.
    /// </summary>
    public class MockVenueConfig
    {
        /// <summary>Venue ID</summary>
        public string VenueId { get; set; } = "MOCK";

        /// <summary>Disable venue (simulate outage)</summary>
        public bool DisableVenue { get; set; } = false;

        /// <summary>Median latency in microseconds</summary>
        public double LatencyMedianUs { get; set; } = 50;

        /// <summary>Latency standard deviation (as multiplier)</summary>
        public double LatencyStdDev { get; set; } = 0.3;

        /// <summary>Random seed for deterministic behavior</summary>
        public int RandomSeed { get; set; } = 12345;

        /// <summary>Probability of instant fill for market orders</summary>
        public double InstantFillProbability { get; set; } = 0.95;

        /// <summary>Maximum orders per second</summary>
        public int MaxOrdersPerSecond { get; set; } = 1000;

        /// <summary>Maximum order size</summary>
        public int MaxOrderSize { get; set; } = 100000;

        /// <summary>Price band percentage (0 = no band)</summary>
        public double PriceBandPercent { get; set; } = 0;

        /// <summary>Default implementation shortfall (bps)</summary>
        public double DefaultShortfallBps { get; set; } = 5.0;

        /// <summary>Default spread (bps)</summary>
        public double DefaultSpreadBps { get; set; } = 2.0;

        /// <summary>Fee schedule</summary>
        public VenueFeeSchedule? FeeSchedule { get; set; }

        /// <summary>Initial liquidity setup</summary>
        public MockLiquiditySetup? InitialLiquidity { get; set; }
    }

    /// <summary>
    /// Mock liquidity setup for order book initialization.
    /// </summary>
    public class MockLiquiditySetup
    {
        /// <summary>Mid price</summary>
        public long MidPrice { get; set; } = 1000000; // $100.00 in ticks

        /// <summary>Spread in ticks</summary>
        public int Spread { get; set; } = 100; // $0.01

        /// <summary>Tick size in ticks</summary>
        public int TickSize { get; set; } = 1; // $0.0001

        /// <summary>Number of bid levels</summary>
        public int BidLevels { get; set; } = 5;

        /// <summary>Number of ask levels</summary>
        public int AskLevels { get; set; } = 5;

        /// <summary>Base size at each level</summary>
        public int BaseSize { get; set; } = 500;
    }
}

