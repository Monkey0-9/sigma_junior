using System;
using System.Threading;
using System.Threading.Tasks;
using Hft.Core;

namespace Hft.Backtest
{
    public sealed class SimpleMatchingEngine : IDisposable
    {
        private readonly LockFreeRingBuffer<Order> _inputRing;
        private readonly PnlEngine _pnlEngine;
        private readonly MetricsCounter _fillsGenerated;
        private readonly CancellationTokenSource _cts;
        private Task? _task;
        private long _fillIdCounter;

        public SimpleMatchingEngine(LockFreeRingBuffer<Order> inputRing, PnlEngine pnlEngine, MetricsCounter fillsGenerated)
        {
            _inputRing = inputRing;
            _pnlEngine = pnlEngine;
            _fillsGenerated = fillsGenerated;
            _cts = new CancellationTokenSource();
        }

        public void Start()
        {
            _task = Task.Run(RunLoop);
        }

        public void Stop()
        {
            _cts.Cancel();
            try { _task?.Wait(); } catch (AggregateException) { }
            catch (TaskCanceledException) { }
        }

        public void ProcessSingleOrder(Order order)
        {
            MockMatch(order);
        }

        private void RunLoop()
        {
            var spinWait = new SpinWait();
            while (!_cts.IsCancellationRequested)
            {
                if (_inputRing.TryRead(out var order))
                {
                    MockMatch(order);
                }
                else
                {
                    spinWait.SpinOnce();
                }
            }
        }

        private void MockMatch(Order order)
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
            _fillsGenerated.Increment();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}

