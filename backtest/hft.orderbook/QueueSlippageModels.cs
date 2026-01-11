using System;
using System.Runtime.CompilerServices;

namespace Hft.OrderBook
{
    /// <summary>
    /// Queue position modeling for passive order fill estimation.
    /// 
    /// Provides:
    /// - Queue position calculation (orders ahead, quantity ahead)
    /// - Time-to-fill estimation based on market activity
    /// - Fill probability estimation
    /// 
    /// Performance: O(1) for most operations.
    /// </summary>
    public sealed class QueuePositionModel
    {
        /// <summary>
        /// Estimate of trades per second at the front of the queue.
        /// Used for time-to-fill calculations.
        /// </summary>
        public double EstimatedTradeRate { get; set; } = 1000.0;

        /// <summary>
        /// Average trade size in ticks.
        /// Used to estimate how quickly quantity ahead will be consumed.
        /// </summary>
        public double AverageTradeSize { get; set; } = 100;

        /// <summary>
        /// Gets the queue position for an order (1 = front).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetQueuePosition(OrderBook book, long orderId)
        {
            return book.GetQueuePosition(orderId);
        }

        /// <summary>
        /// Gets the number of orders ahead of an order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetOrdersAhead(OrderBook book, long orderId)
        {
            return book.GetOrdersAhead(orderId);
        }

        /// <summary>
        /// Gets the total quantity ahead of an order in the queue.
        /// This includes both visible and hidden orders.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetQuantityAhead(OrderBook book, long orderId)
        {
            return book.GetQuantityAhead(orderId);
        }

        /// <summary>
        /// Estimates the time until an order will be filled.
        /// Based on historical trade rate and average trade size.
        /// 
        /// Returns: Estimated time in microseconds, or -1 if cannot estimate.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double EstimateTimeToFill(OrderBook book, long orderId, double tradeRateMultiplier = 1.0)
        {
            var order = book.GetOrder(orderId);
            if (order == null || !order.Value.IsActive)
                return -1;

            int qtyAhead = GetQuantityAhead(book, orderId);
            if (qtyAhead == 0)
                return 0; // At front of queue

            // Estimate trades needed to consume quantity ahead
            double avgTrade = AverageTradeSize * tradeRateMultiplier;
            if (avgTrade <= 0)
                avgTrade = AverageTradeSize;

            double tradesNeeded = qtyAhead / avgTrade;

            // Estimate time based on trade rate
            double rate = EstimatedTradeRate * tradeRateMultiplier;
            if (rate <= 0)
                rate = EstimatedTradeRate;

            // Time = trades / rate (convert to microseconds)
            double timeMicros = (tradesNeeded / rate) * 1_000_000.0;

            return timeMicros;
        }

        /// <summary>
        /// Estimates the probability that an order will be filled within a given time.
        /// Uses exponential decay model based on trade rate.
        /// 
        /// Returns: Probability between 0 and 1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double EstimateFillProbability(OrderBook book, long orderId, double timeWindowSeconds)
        {
            var order = book.GetOrder(orderId);
            if (order == null || !order.Value.IsActive)
                return 0;

            int qtyAhead = GetQuantityAhead(book, orderId);
            if (qtyAhead == 0)
                return 1.0; // At front, will fill immediately

            // Calculate how much quantity we expect to be traded in the time window
            double expectedVolume = EstimatedTradeRate * AverageTradeSize * timeWindowSeconds;

            if (expectedVolume <= 0)
                return 0.5; // Unknown

            // Probability based on how much of the ahead quantity will be consumed
            double ratio = Math.Min(1.0, expectedVolume / qtyAhead);

            // Use exponential decay for more realistic probability
            return 1.0 - Math.Exp(-ratio * 2.0);
        }

        /// <summary>
        /// Calculates the expected fill price for a market order.
        /// Uses volume-weighted average price through the book.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double? EstimateFillPrice(OrderBook book, OrderSide side, int quantity)
        {
            var bbo = book.GetBestBidAsk();
            if (!bbo.HasBothSides)
                return null;

            long totalCost = 0;
            int remainingQty = quantity;
            long startPrice = side == OrderSide.Buy ? bbo.BestAskPrice : bbo.BestBidPrice;

            if (side == OrderSide.Buy)
            {
                // Walk up the asks
                var entries = new OrderBookEntry[100]; // Max depth
                book.GetBestAsks(entries);

                foreach (var entry in entries)
                {
                    if (entry.TotalQuantity == 0) break;

                    int matchQty = Math.Min(remainingQty, entry.TotalQuantity);
                    totalCost += matchQty * entry.Price;
                    remainingQty -= matchQty;

                    if (remainingQty == 0) break;
                }
            }
            else
            {
                // Walk down the bids
                var entries = new OrderBookEntry[100];
                book.GetBestBids(entries);

                foreach (var entry in entries)
                {
                    if (entry.TotalQuantity == 0) break;

                    int matchQty = Math.Min(remainingQty, entry.TotalQuantity);
                    totalCost += matchQty * entry.Price;
                    remainingQty -= matchQty;

                    if (remainingQty == 0) break;
                }
            }

            if (remainingQty > 0)
                return null; // Not enough liquidity

            return (double)totalCost / quantity;
        }

        /// <summary>
        /// Updates the model with observed trade activity.
        /// Call this after each trade for adaptive estimation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnTrade(double tradeSize)
        {
            // Exponential moving average for trade size
            double alpha = 0.1; // Smoothing factor
            AverageTradeSize = alpha * tradeSize + (1 - alpha) * AverageTradeSize;
        }

        /// <summary>
        /// Updates the model with observed trade rate.
        /// Call this periodically (e.g., every second).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateTradeRate(double tradesPerSecond)
        {
            // Exponential moving average for trade rate
            double alpha = 0.1; // Smoothing factor
            EstimatedTradeRate = alpha * tradesPerSecond + (1 - alpha) * EstimatedTradeRate;
        }
    }

    /// <summary>
    /// Slippage and market impact model for execution quality estimation.
    /// 
    /// Provides deterministic slippage calculation based on:
    /// - Order size relative to available liquidity
    /// - Volatility proxy (bid-ask spread)
    /// - Market depth profile
    /// 
    /// References:
    /// - Almgren-Chriss model for temporary impact
    /// - Obizhaeva-Wang model for permanent impact
    /// </summary>
    public sealed class SlippageModel
    {
        /// <summary>
        /// Temporary impact coefficient (sigma * lambda).
        /// Higher values = more impact from aggressive trading.
        /// Typical values: 0.001 - 0.01 for equities.
        /// </summary>
        public double TemporaryImpactCoefficient { get; set; } = 0.005;

        /// <summary>
        /// Permanent impact coefficient.
        /// Typical values: 0.1 - 0.5 of temporary impact.
        /// </summary>
        public double PermanentImpactCoefficient { get; set; } = 0.001;

        /// <summary>
        /// Volatility proxy (annualized, as fraction).
        /// Typical values: 0.2 - 0.5 for equities.
        /// </summary>
        public double Volatility { get; set; } = 0.25;

        /// <summary>
        /// Time horizon for impact decay (in days).
        /// </summary>
        public double ImpactDecayDays { get; set; } = 1.0;

        /// <summary>
        /// Calculates the expected slippage for an order.
        /// 
        /// Uses a simplified market impact model:
        /// Impact = sigma * lambda * (Q / ADV) ^ beta
        /// 
        /// Where:
        /// - sigma: Volatility
        /// - lambda: Temporary impact coefficient
        /// - Q: Order quantity
        /// - ADV: Average daily volume
        /// - beta: Impact exponent (typically 0.5-1.0)
        /// </summary>
        /// <param name="order">The order to estimate slippage for</param>
        /// <param name="book">Current order book state</param>
        /// <param name="averageDailyVolume">ADV for the instrument (default 1M)</param>
        /// <returns>Expected slippage as a fraction (e.g., 0.001 = 10 bps)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double CalculateSlippage(
            OrderQueueEntry order,
            OrderBook book,
            double averageDailyVolume = 1_000_000)
        {
            if (order.LeavesQuantity <= 0)
                return 0;

            var bbo = book.GetBestBidAsk();
            if (!bbo.HasBothSides)
                return 0;

            // Calculate spread as volatility proxy
            double spread = bbo.Spread;
            double midPrice = bbo.MidPrice;
            double spreadBps = spread > 0 ? (spread / midPrice) * 10000 : 1; // bps

            // Calculate relative order size (participation rate)
            double participationRate = (double)order.LeavesQuantity / averageDailyVolume;

            // Impact exponent (0.5 for square root law, 1.0 for linear)
            double beta = 0.5;

            // Calculate temporary impact
            double tempImpact = TemporaryImpactCoefficient * Math.Pow(participationRate, beta);

            // Calculate permanent impact
            double permImpact = PermanentImpactCoefficient * Math.Pow(participationRate, beta);

            // Total slippage (temporary + permanent + half spread)
            double totalSlippage = tempImpact + permImpact + (spreadBps / 10000.0) * 0.5;

            return totalSlippage;
        }

        /// <summary>
        /// Calculates the expected execution price for an order.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double CalculateExecutionPrice(
            OrderQueueEntry order,
            OrderBook book,
            double averageDailyVolume = 1_000_000)
        {
            var bbo = book.GetBestBidAsk();
            if (!bbo.HasBothSides)
                return 0;

            double slippage = CalculateSlippage(order, book, averageDailyVolume);
            double midPrice = bbo.MidPrice;

            if (order.Side == OrderSide.Buy)
            {
                // Buy order: price moves up
                return midPrice * (1 + slippage);
            }
            else
            {
                // Sell order: price moves down
                return midPrice * (1 - slippage);
            }
        }

        /// <summary>
        /// Calculates the market impact for a trade.
        /// Returns impact in price terms.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double CalculateMarketImpact(
            int tradeQuantity,
            OrderSide side,
            OrderBook book,
            double averageDailyVolume = 1_000_000)
        {
            var bbo = book.GetBestBidAsk();
            if (!bbo.HasBothSides)
                return 0;

            double midPrice = bbo.MidPrice;
            double participationRate = (double)tradeQuantity / averageDailyVolume;

            // Temporary impact only (permanent impact is realized over time)
            double impact = TemporaryImpactCoefficient * Math.Pow(participationRate, 0.5) * midPrice;

            return side == OrderSide.Buy ? impact : -impact;
        }

        /// <summary>
        /// Calculates slippage for a market order given current depth.
        /// More accurate for immediate execution estimates.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double CalculateDepthBasedSlippage(
            OrderSide side,
            int quantity,
            OrderBook book)
        {
            var bbo = book.GetBestBidAsk();
            if (!bbo.HasBothSides)
                return 0;

            long totalCost = 0;
            int remainingQty = quantity;
            int filledQty = 0;

            if (side == OrderSide.Buy)
            {
                var entries = new OrderBookEntry[100];
                book.GetBestAsks(entries);

                foreach (var entry in entries)
                {
                    if (entry.TotalQuantity == 0) break;

                    int matchQty = Math.Min(remainingQty, entry.TotalQuantity);
                    totalCost += matchQty * entry.Price;
                    remainingQty -= matchQty;
                    filledQty += matchQty;

                    if (remainingQty == 0) break;
                }
            }
            else
            {
                var entries = new OrderBookEntry[100];
                book.GetBestBids(entries);

                foreach (var entry in entries)
                {
                    if (entry.TotalQuantity == 0) break;

                    int matchQty = Math.Min(remainingQty, entry.TotalQuantity);
                    totalCost += matchQty * entry.Price;
                    remainingQty -= matchQty;
                    filledQty += matchQty;

                    if (remainingQty == 0) break;
                }
            }

            if (filledQty == 0)
                return 0;

            double vwap = (double)totalCost / filledQty;
            double referencePrice = side == OrderSide.Buy ? bbo.BestAskPrice : bbo.BestBidPrice;

            return Math.Abs((vwap - referencePrice) / referencePrice);
        }
    }

    /// <summary>
    /// Latency injector for simulating exchange delays.
    /// 
    /// Provides deterministic latency simulation for:
    /// - Per-venue latency distributions
    /// - Network delay modeling
    /// - Order processing delays
    /// 
    /// Uses seeded random for deterministic replay.
    /// </summary>
    public sealed class LatencyInjector
    {
        private readonly int _seed;
        private Random _random;

        /// <summary>
        /// Creates a new latency injector.
        /// </summary>
        /// <param name="seed">Seed for deterministic random (0 = use time)</param>
        public LatencyInjector(int seed = 12345)
        {
            _seed = seed == 0 ? Environment.TickCount : seed;
            _random = new Random(_seed);
        }

        /// <summary>
        /// Resets the random state for deterministic replay.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _random = new Random(_seed);
        }

        /// <summary>
        /// Generates a latency sample from a log-normal distribution.
        /// 
        /// Log-normal is commonly used for network latency modeling because:
        /// - Skewed right distribution (long tail)
        /// - Always positive
        /// - Matches observed network delay patterns
        /// 
        /// Parameters are typically calibrated from exchange data.
        /// </summary>
        /// <param name="mu">Location parameter (mean of underlying normal)</param>
        /// <param name="sigma">Scale parameter (std dev of underlying normal)</param>
        /// <returns>Latency in microseconds</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double SampleLogNormal(double mu, double sigma)
        {
            // Box-Muller transform for normal distribution
            double u1 = 1.0 - _random.NextDouble();
            double u2 = 1.0 - _random.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

            // Transform to log-normal
            return Math.Exp(mu + sigma * z);
        }

        /// <summary>
        /// Samples from a normal distribution.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double SampleNormal(double mean, double stdDev)
        {
            double u1 = 1.0 - _random.NextDouble();
            double u2 = 1.0 - _random.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return mean + z * stdDev;
        }

        /// <summary>
        /// Samples from an exponential distribution.
        /// Useful for modeling inter-arrival times.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double SampleExponential(double rate)
        {
            return -Math.Log(1.0 - _random.NextDouble()) / rate;
        }

        /// <summary>
        /// Gets latency for a specific venue.
        /// Returns microseconds.
        /// 
        /// Typical values (calibrated from exchange data):
        /// - NASDAQ: mu=3.9, sigma=0.3 (~50μs median)
        /// - NYSE: mu=4.4, sigma=0.4 (~80μs median)
        /// - CME: mu=5.3, sigma=0.5 (~200μs median)
        /// - ARCA: mu=4.0, sigma=0.35 (~55μs median)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetVenueLatency(string venue)
        {
            return venue.ToUpperInvariant() switch
            {
                "NASDAQ" or "Nasdaq" or "Q" => SampleLogNormal(3.9, 0.3),
                "NYSE" or "Nyse" or "N" => SampleLogNormal(4.4, 0.4),
                "CME" or "Cme" or "G" => SampleLogNormal(5.3, 0.5),
                "ARCA" or "Arca" or "A" => SampleLogNormal(4.0, 0.35),
                "EDGEA" or "EdgeA" => SampleLogNormal(3.8, 0.25),
                "BATS" or "Bats" or "Z" => SampleLogNormal(3.7, 0.3),
                "IEX" or "Iex" => SampleLogNormal(4.2, 0.35),
                _ => SampleLogNormal(4.0, 0.4) // Default
            };
        }

        /// <summary>
        /// Gets latency for a venue with specified parameters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetVenueLatency(string venue, double medianMicroseconds, double stdDevPercent = 0.3)
        {
            // Convert median to mu for log-normal
            double mu = Math.Log(medianMicroseconds);
            double sigma = stdDevPercent; // Relative standard deviation

            return venue.ToUpperInvariant() switch
            {
                "NASDAQ" => SampleLogNormal(mu, sigma),
                "NYSE" => SampleLogNormal(mu, sigma),
                "CME" => SampleLogNormal(mu, sigma),
                _ => SampleLogNormal(mu, sigma)
            };
        }

        /// <summary>
        /// Applies latency to an event timestamp.
        /// Returns the new timestamp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ApplyLatency(long timestamp, string venue)
        {
            double latencyUs = GetVenueLatency(venue);
            return timestamp + (long)(latencyUs * 1000); // Convert μs to ns
        }

        /// <summary>
        /// Applies latency to an event timestamp with custom parameters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ApplyLatency(long timestamp, string venue, double medianMicroseconds, double stdDevPercent = 0.3)
        {
            double latencyUs = GetVenueLatency(venue, medianMicroseconds, stdDevPercent);
            return timestamp + (long)(latencyUs * 1000);
        }

        /// <summary>
        /// Creates a latency distribution preset for common venues.
        /// </summary>
        public static LatencyPreset GetPreset(string venue)
        {
            return venue.ToUpperInvariant() switch
            {
                "NASDAQ" => new LatencyPreset("NASDAQ", 50, 0.3),
                "NYSE" => new LatencyPreset("NYSE", 80, 0.35),
                "CME" => new LatencyPreset("CME", 200, 0.4),
                "ARCA" => new LatencyPreset("ARCA", 55, 0.3),
                "EDGEA" => new LatencyPreset("EDGEA", 45, 0.25),
                "BATS" => new LatencyPreset("BATS", 40, 0.3),
                "IEX" => new LatencyPreset("IEX", 65, 0.35),
                _ => new LatencyPreset(venue, 50, 0.4)
            };
        }
    }

    /// <summary>
    /// Latency distribution preset for a venue.
    /// </summary>
    public readonly struct LatencyPreset
    {
        public string Venue { get; }
        public double MedianMicroseconds { get; }
        public double StdDevPercent { get; }

        public LatencyPreset(string venue, double medianMicroseconds, double stdDevPercent)
        {
            Venue = venue;
            MedianMicroseconds = medianMicroseconds;
            StdDevPercent = stdDevPercent;
        }
    }
}

