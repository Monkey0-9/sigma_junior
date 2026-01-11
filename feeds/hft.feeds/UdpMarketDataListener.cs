using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Hft.Core;
using Hft.Core.RingBuffer; // NEW namespace

namespace Hft.Feeds
{
    public class UdpMarketDataListener
    {
        private readonly int _port;
        private readonly LockFreeRingBuffer<MarketDataTick> _ringBuffer;
        private readonly CancellationTokenSource _cts;
        private Task? _task;

        public UdpMarketDataListener(int port, LockFreeRingBuffer<MarketDataTick> ringBuffer)
        {
            _port = port;
            _ringBuffer = ringBuffer;
            _cts = new CancellationTokenSource();
        }

        // ... existing implementation remains same, just constructor type updated via namespace ...

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
            using var client = new UdpClient(_port);
            var endPoint = new IPEndPoint(IPAddress.Any, _port);

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    byte[] data = client.Receive(ref endPoint);

                    if (data.Length >= Marshal.SizeOf<MarketDataTick>())
                    {
                        var tick = MemoryMarshal.Read<MarketDataTick>(data);
                        _ringBuffer.TryWrite(in tick);
                    }
                }
                catch (Exception)
                {
                    if (_cts.IsCancellationRequested) break;
                }
            }
        }
    }
}
