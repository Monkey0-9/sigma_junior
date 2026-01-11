using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Hft.Core;

namespace Hft.Feeds
{
    public class UdpMarketDataSimulator
    {
        private readonly int _port;
        private readonly int _delayMs;
        private readonly CancellationTokenSource _cts;
        private Task? _task;

        public UdpMarketDataSimulator(int port, int delayMs)
        {
            _port = port;
            _delayMs = delayMs;
            _cts = new CancellationTokenSource();
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
            using var client = new UdpClient();
            var endPoint = new IPEndPoint(IPAddress.Loopback, _port);
            var random = new Random(12345); // Deterministic Seed
            int seq = 0;
            double price = 100.0;

            while (!_cts.IsCancellationRequested)
            {
                price += (random.NextDouble() - 0.5) * 0.1;
                double spread = 0.05;
                double bid = price - spread / 2;
                double ask = price + spread / 2;

                var tick = MarketDataTick.CreateQuote(bid, ask, 100, 100, 1001, ++seq);

                // Marshal to bytes
                int size = Marshal.SizeOf<MarketDataTick>();
                byte[] data = new byte[size];

                // Use unsafe marshaling for speed/correctness in specific memory layout
                IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(tick, ptr, false);
                    Marshal.Copy(ptr, data, 0, size);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }

                client.Send(data, data.Length, endPoint);
                Thread.Sleep(_delayMs);
            }
        }
    }
}
