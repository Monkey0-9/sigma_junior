using System;
using System.Threading;
using System.Threading.Tasks;
using Hft.Core;
using Hft.Core.Audit;
using Hft.Core.RingBuffer; // NEW namespace

namespace Hft.Risk
{
    public class PreTradeRiskEngine
    {
        private readonly LockFreeRingBuffer<Order> _inputRing;
        private readonly LockFreeRingBuffer<Order> _outputRing;
        private readonly PositionSnapshot _position;
        private readonly MetricsCounter _ordersApproved;
        private readonly MetricsCounter _ordersRejected;
        private readonly BinaryAuditLog _auditLog;
        // ObjectPool removed
        private readonly CancellationTokenSource _cts;
        private RiskLimits _limits;
        private Task _task;

        private long _lastSecondTimestamp;
        private int _ordersThisSecond;

        public PreTradeRiskEngine(
            LockFreeRingBuffer<Order> inputRing,
            LockFreeRingBuffer<Order> outputRing,
            PositionSnapshot position,
            MetricsCounter ordersApproved,
            MetricsCounter ordersRejected,
            RiskLimits limits,
            BinaryAuditLog auditLog)
        {
            _inputRing = inputRing;
            _outputRing = outputRing;
            _position = position;
            _ordersApproved = ordersApproved;
            _ordersRejected = ordersRejected;
            _limits = limits;
            _auditLog = auditLog;
            _cts = new CancellationTokenSource();
            _lastSecondTimestamp = DateTime.UtcNow.Ticks;
        }

        public void UpdateLimits(RiskLimits newLimits)
        {
            _limits = newLimits;
        }

        public void Start()
        {
            _task = Task.Run(RunLoop);
        }

        public void Stop()
        {
            _cts.Cancel();
            try { _task?.Wait(); } catch { }
        }

        private void RunLoop()
        {
            var spinWait = new SpinWait();
            while (!_cts.IsCancellationRequested)
            {
                if (_inputRing.TryRead(out var order))
                {
                    // Order is struct (copy).
                    if (CheckRisk(in order))
                    {
                        if (_outputRing.TryWrite(in order))
                        {
                            _ordersApproved.Increment();
                            _auditLog.LogOrder(order, AuditRecordType.OrderSubmit);
                        }
                        else
                        {
                            _ordersRejected.Increment();
                            _auditLog.LogOrder(order, AuditRecordType.OrderReject);
                        }
                    }
                    else
                    {
                        // order.IsActive = false; // Modifies local copy, useless unless logged.
                        _ordersRejected.Increment();
                        _auditLog.LogOrder(order, AuditRecordType.OrderReject);
                    }
                }
                else
                {
                    spinWait.SpinOnce();
                }
            }
        }

        private bool CheckRisk(in Order order)
        {
            if (_limits.KillSwitchActive) return false;
            if (order.Quantity > _limits.MaxOrderQty) return false;
            // potential overflow if price * qty is huge, but double covers it
            if (order.Quantity * order.Price > _limits.MaxNotionalPerOrder) return false;

            double currentPos = _position.NetPosition;
            double projectedPos = order.Side == OrderSide.Buy ? currentPos + order.Quantity : currentPos - order.Quantity;
            if (Math.Abs(projectedPos) > _limits.MaxPosition) return false;

            long now = DateTime.UtcNow.Ticks;
            if (now - _lastSecondTimestamp > TimeSpan.TicksPerSecond)
            {
                _lastSecondTimestamp = now;
                _ordersThisSecond = 0;
            }
            if (_ordersThisSecond >= _limits.MaxOrdersPerSec) return false;
            _ordersThisSecond++;

            return true;
        }
    }
}
