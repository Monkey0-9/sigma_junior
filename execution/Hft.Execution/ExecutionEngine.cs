using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Hft.Core;

namespace Hft.Execution
{
    /// <summary>
    /// Institutional Execution Engine with micro-structure simulation.
    /// GRANDMASTER: Proper Task initialization, CancellationToken lifecycle, and dispose pattern.
    /// </summary>
    public sealed class ExecutionEngine : IDisposable
    {
        private readonly LockFreeRingBuffer<Order> _inputRing;
        private readonly PnlEngine _pnlEngine;
        private readonly MetricsCounter _executedOrders;
        private readonly IEventLogger _logger;
        private readonly CancellationTokenSource _cts;
        private Task? _task;
        private long _fillIdCounter;
        private bool _disposed;

        // GRANDMASTER: Simulation parameters for realistic backtesting
        public double LatencyMeanMs { get; set; } = 2.0;
        public double LatencyStdDevMs { get; set; } = 0.5;
        public double FillProbability { get; set; } = 0.95; // Simulates adverse selection or partial fills

        private readonly ConcurrentQueue<PendingExecution> _pendingExecutions = new();
        // GRANDMASTER: Cryptographic RNG for security-compliant randomness (CA5394)
        private readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();

        public ExecutionEngine(
            LockFreeRingBuffer<Order> inputRing,
            PnlEngine pnlEngine,
            MetricsCounter executedOrders,
            IEventLogger logger)
        {
            _inputRing = inputRing ?? throw new ArgumentNullException(nameof(inputRing));
            _pnlEngine = pnlEngine ?? throw new ArgumentNullException(nameof(pnlEngine));
            _executedOrders = executedOrders ?? throw new ArgumentNullException(nameof(executedOrders));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cts = new CancellationTokenSource();
            _fillIdCounter = 0;
        }

        /// <summary>
        /// Starts the execution engine. Thread-safe for single Start/Stop lifecycle.
        /// </summary>
        public void Start()
        {
            // GRANDMASTER: CA1513 - Use ObjectDisposedException.ThrowIf for proper exception handling
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_task != null && !_task.IsCompleted) return; // Already running

            _task = Task.Run(RunLoop);
        }

        /// <summary>
        /// Stops the engine and waits for graceful shutdown.
        /// </summary>
        public void Stop()
        {
            _cts.Cancel();
            try
            {
                _task?.Wait(TimeSpan.FromSeconds(5)); // Graceful timeout
            }
            catch (AggregateException) when (_cts.IsCancellationRequested)
            {
                // Expected during cancellation
            }
            catch (TimeoutException)
            {
                // Task didn't complete in time
            }
        }

        private async Task RunLoop()
        {
            var spinWait = new SpinWait();
            while (!_cts.IsCancellationRequested)
            {
                // 1. Ingest new orders
                if (_inputRing.TryRead(out var order))
                {
                    // Calculate a realistic latency using a normal distribution approximation
                    double latency = LatencyMeanMs + (LatencyStdDevMs * (2 * NextRandomDouble() - 1));
                    long releaseTimeTicks = DateTime.UtcNow.Ticks + (long)(latency * TimeSpan.TicksPerMillisecond);

                    _pendingExecutions.Enqueue(new PendingExecution(order, releaseTimeTicks));
                }

                // 2. Process pending executions that reached their "release time"
                while (_pendingExecutions.TryPeek(out var pending) && DateTime.UtcNow.Ticks >= pending.ReleaseTimeTicks)
                {
                    if (_pendingExecutions.TryDequeue(out var ready))
                    {
                        ProcessExecution(ready.Order);
                    }
                }

                if (_pendingExecutions.IsEmpty)
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    // Prevent busy waiting if there's nothing to do but wait for latency
                    // GRANDMASTER: CA2007 - ConfigureAwait(false) for library code
                    await Task.Delay(1, _cts.Token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// GRANDMASTER: Cryptographic random for security compliance (CA5394).
        /// </summary>
        private double NextRandomDouble()
        {
            byte[] randomBytes = new byte[8];
            _rng.GetBytes(randomBytes);
            return BitConverter.ToUInt64(randomBytes, 0) / (double)ulong.MaxValue;
        }

        private void ProcessExecution(Order order)
        {
            using var activity = HftTracing.Source.StartActivity(HftTracing.OrderExecution);
            activity?.SetTag("hft.order_id", order.OrderId);
            activity?.SetTag("hft.instrument_id", order.InstrumentId);

            // Simulate Partial Fills
            double qtyToFill = order.Quantity;
            if (NextRandomDouble() > FillProbability)
            {
                qtyToFill = Math.Floor(order.Quantity * NextRandomDouble());
                if (qtyToFill <= 0)
                {
                    _logger.LogRiskEvent("Venue", "Cancel", $"Order {order.OrderId} cancelled by venue (No liquidity)");
                    CentralMetricsStore.Instance.GetCounter("hft_execution_cancelled").Increment();
                    return;
                }
                _logger.LogInfo("EXECUTION", $"Partial fill for Order {order.OrderId}: {qtyToFill}/{order.Quantity}");
            }

            var fill = Fill.CreateWithId(
                Interlocked.Increment(ref _fillIdCounter),
                order.OrderId,
                order.InstrumentId,
                order.Side,
                order.Price,
                (int)qtyToFill,
                DateTime.UtcNow.Ticks);

            _pnlEngine.OnFill(fill);
            _executedOrders.Increment();
            CentralMetricsStore.Instance.GetCounter("hft_fills_total").Increment();
            _logger.LogFill(in fill);
        }

        /// <summary>
        /// GRANDMASTER: Proper dispose pattern with SuppressFinalize.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cts.Cancel();
            _cts.Dispose();
            _rng.Dispose();
            GC.SuppressFinalize(this);
        }

        private readonly struct PendingExecution
        {
            public Order Order { get; }
            public long ReleaseTimeTicks { get; }

            public PendingExecution(Order order, long releaseTimeTicks)
            {
                Order = order;
                ReleaseTimeTicks = releaseTimeTicks;
            }
        }
    }
}

