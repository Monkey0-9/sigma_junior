using System;
using System.Threading;
using System.Threading.Tasks;
using Hft.Core;
using Hft.Core.RingBuffer; // NEW namespace

namespace Hft.Execution
{
    public class ExecutionStub
    {
        private readonly LockFreeRingBuffer<Order> _inputRing;
        private readonly PnlEngine _pnlEngine;
        private readonly MetricsCounter _executedOrders;
        // ObjectPool removed
        private readonly CancellationTokenSource _cts;
        private Task _task;
        private long _fillIdCounter;

        public ExecutionStub(
            LockFreeRingBuffer<Order> inputRing,
            PnlEngine pnlEngine,
            MetricsCounter executedOrders)
        {
            _inputRing = inputRing;
            _pnlEngine = pnlEngine;
            _executedOrders = executedOrders;
            _cts = new CancellationTokenSource();
            _fillIdCounter = 0;
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
                    var fill = Fill.CreateWithId(
                        Interlocked.Increment(ref _fillIdCounter),
                        order.OrderId,
                        order.InstrumentId,
                        order.Side,
                        order.Price,
                        (int)order.Quantity,
                        DateTime.UtcNow.Ticks);

                    _pnlEngine.OnFill(fill);
                    _executedOrders.Increment();

                    // No return to pool needed for struct
                }
                else
                {
                    spinWait.SpinOnce();
                }
            }
        }
    }
}
