using System;
using System.Threading;
using System.Threading.Tasks;
using Hft.Core;
using Hft.Core.Audit;
using Hft.Infra;

namespace Hft.Risk
{
    /// <summary>
    /// Pre-Trade Risk Engine for order validation and rate limiting.
    /// GRANDMASTER: Proper Task initialization, CancellationToken lifecycle, and dispose pattern.
    /// </summary>
    public sealed class PreTradeRiskEngine : IDisposable
    {
        private readonly LockFreeRingBuffer<Order> _inputRing;
        private readonly LockFreeRingBuffer<Order> _outputRing;
        private readonly PositionSnapshot _position;
        private readonly MetricsCounter _ordersApproved;
        private readonly MetricsCounter _ordersRejected;
        private readonly IEventLogger _logger;
        private readonly CancellationTokenSource _cts;
        private RiskLimits _limits;
        private Task? _task;
        private bool _disposed;

        private long _lastSecondTimestamp;
        private int _ordersThisSecond;

        public PreTradeRiskEngine(
            LockFreeRingBuffer<Order> inputRing,
            LockFreeRingBuffer<Order> outputRing,
            PositionSnapshot position,
            MetricsCounter ordersApproved,
            MetricsCounter ordersRejected,
            RiskLimits limits,
            IEventLogger logger)
        {
            _inputRing = inputRing ?? throw new ArgumentNullException(nameof(inputRing));
            _outputRing = outputRing ?? throw new ArgumentNullException(nameof(outputRing));
            _position = position ?? throw new ArgumentNullException(nameof(position));
            _ordersApproved = ordersApproved ?? throw new ArgumentNullException(nameof(ordersApproved));
            _ordersRejected = ordersRejected ?? throw new ArgumentNullException(nameof(ordersRejected));
            _limits = limits ?? throw new ArgumentNullException(nameof(limits));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cts = new CancellationTokenSource();
            _lastSecondTimestamp = DateTime.UtcNow.Ticks;
        }

        /// <summary>
        /// Updates the risk limits at runtime.
        /// </summary>
        public void UpdateLimits(RiskLimits newLimits)
        {
            _limits = newLimits ?? throw new ArgumentNullException(nameof(newLimits));
        }

        /// <summary>
        /// Starts the risk engine. Thread-safe for single Start/Stop lifecycle.
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

        private void RunLoop()
        {
            var spinWait = new SpinWait();
            while (!_cts.IsCancellationRequested)
            {
                if (_inputRing.TryRead(out var order))
                {
                    var result = CheckRisk(in order);
                    if (result.Decision == RiskDecision.Allow)
                    {
                        if (_outputRing.TryWrite(in order))
                        {
                            _ordersApproved.Increment();
                            _logger.LogOrder(AuditRecordType.OrderSubmit, in order);
                        }
                        else
                        {
                            _ordersRejected.Increment();
                            _logger.LogOrder(AuditRecordType.OrderReject, in order);
                        }
                    }
                    else
                    {
                        // Throttled or Blocked
                        _ordersRejected.Increment();
                        // CheckRisk already logs the rejection reason
                        // But optionally we could log the evidence payload here more structured
                    }
                }
                else
                {
                    spinWait.SpinOnce();
                }
            }
        }

        private RiskCheckResult CheckRisk(in Order order)
        {
            if (_limits.KillSwitchActive)
            {
                _logger.LogRiskEvent("KillSwitch", "Reject", "Global Kill Switch is Active");
                _logger.LogOrder(AuditRecordType.OrderReject, in order);
                return RiskCheckResult.Blocked("KillSwitch", "Global Kill Switch Active", 1, 0);
            }

            // 1. CAPITAL PROTECTION (Global)
            if (_position.RealizedPnL + _position.UnrealizedPnL < -_limits.DailyLossLimit)
            {
                // In production, might be Throttle first? Prompt says Hard Gate.
                var pnl = _position.RealizedPnL + _position.UnrealizedPnL;
                _logger.LogRiskEvent("DailyLoss", "Reject", $"PnL {pnl} < Limit -{_limits.DailyLossLimit}");
                _logger.LogOrder(AuditRecordType.OrderReject, in order);
                return RiskCheckResult.Blocked("DailyLoss", "Daily Loss Limit Exceeded", pnl, -_limits.DailyLossLimit);
            }

            // 2. HIERARCHICAL CHECKS (Symbol vs Global)
            double maxQty = _limits.MaxOrderQty;
            double maxPos = _limits.MaxPosition;
            double maxNotional = _limits.MaxNotionalPerOrder;

            if (_limits.SymbolOverrides.TryGetValue(order.InstrumentId, out var symbolLimit))
            {
                maxQty = symbolLimit.MaxOrderQty;
                maxPos = symbolLimit.MaxPosition;
                maxNotional = symbolLimit.MaxNotionalPerOrder;
            }

            if (order.Quantity > maxQty)
            {
                _logger.LogRiskEvent("MaxOrderQty", "Reject", $"Qty {order.Quantity} > Limit {maxQty}");
                _logger.LogOrder(AuditRecordType.OrderReject, in order);
                return RiskCheckResult.Blocked("MaxOrderQty", "Order Quantity Exceeded", order.Quantity, maxQty);
            }

            if (order.Quantity * order.Price > maxNotional)
            {
                double notional = order.Quantity * order.Price;
                _logger.LogRiskEvent("MaxNotional", "Reject", $"Notional {notional} > Limit {maxNotional}");
                _logger.LogOrder(AuditRecordType.OrderReject, in order);
                return RiskCheckResult.Blocked("MaxNotional", "Notional Exceeded", notional, maxNotional);
            }

            double currentPos = _position.NetPosition;
            double projectedPos = order.Side == OrderSide.Buy ? currentPos + order.Quantity : currentPos - order.Quantity;
            if (Math.Abs(projectedPos) > maxPos)
            {
                 _logger.LogRiskEvent("MaxPosition", "Reject", $"Projected Pos {projectedPos} > Limit {maxPos}");
                 _logger.LogOrder(AuditRecordType.OrderReject, in order);
                 return RiskCheckResult.Blocked("MaxPosition", "Position Limit Exceeded", Math.Abs(projectedPos), maxPos);
            }

            // 3. RATE LIMITING
            long now = DateTime.UtcNow.Ticks;
            if (now - _lastSecondTimestamp > TimeSpan.TicksPerSecond)
            {
                _lastSecondTimestamp = now;
                _ordersThisSecond = 0;
            }
            if (_ordersThisSecond >= _limits.MaxOrdersPerSec)
            {
                _logger.LogRiskEvent("MaxOrdersPerSec", "Reject", $"Rate Limit {_limits.MaxOrdersPerSec} exceeded");
                _logger.LogOrder(AuditRecordType.OrderReject, in order);
                return RiskCheckResult.Throttled("MaxOrdersPerSec", "Rate Limit Exceeded");
            }
            _ordersThisSecond++;

            return RiskCheckResult.Allowed();
        }

        /// <summary>
        /// GRANDMASTER: Proper dispose pattern with SuppressFinalize.
        /// </summary>
        private void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                _cts.Cancel();
                _cts.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}

