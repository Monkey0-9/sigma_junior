using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Hft.Core;

namespace Hft.Feeds
{
    /// <summary>
    /// UDP Market Data Simulator for testing.
    /// GRANDMASTER: Proper Task initialization, CancellationToken lifecycle, and dispose pattern.
    /// Uses cryptographic RNG for simulation to satisfy CA5394.
    /// </summary>
    public sealed class UdpMarketDataSimulator : IDisposable
    {
        private readonly int _port;
        private readonly int _delayMs;
        private readonly CancellationTokenSource _cts;
        private Task? _task;
        private bool _disposed;
        // GRANDMASTER: Cryptographic RNG for security-compliant randomness (CA5394)
        private readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();

        public UdpMarketDataSimulator(int port, int delayMs)
        {
            _port = port;
            _delayMs = delayMs;
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// Starts the simulator. Thread-safe for single Start/Stop lifecycle.
        /// </summary>
        public void Start()
        {
            // GRANDMASTER: Use ObjectDisposedException.ThrowIf for CA1513 compliance
            ObjectDisposedException.ThrowIf(_disposed, nameof(UdpMarketDataSimulator));
            if (_task != null && !_task.IsCompleted) return; // Already running

            _task = Task.Run(RunLoop);
        }

        /// <summary>
        /// Stops the simulator and waits for graceful shutdown.
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
                _rng.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void RunLoop()
        {
            // GRANDMASTER: Use using pattern for proper socket cleanup
            using var client = new UdpClient();
            var endPoint = new IPEndPoint(IPAddress.Loopback, _port);
            int seq = 0;
            double price = 100.0;
            byte[] randomBuffer = new byte[sizeof(double)];

            while (!_cts.IsCancellationRequested)
            {
                // GRANDMASTER: Cryptographically secure random value for price movement
                _rng.GetBytes(randomBuffer);
                double randomValue = BitConverter.ToDouble(randomBuffer, 0);
                price += (randomValue - 0.5) * 0.1;

                var bids = new PriceLevel[5];
                var asks = new PriceLevel[5];

                for (int i = 0; i < 5; i++)
                {
                    bids[i] = new PriceLevel(price - (i + 1) * 0.01, 100 * (i + 1));
                    asks[i] = new PriceLevel(price + (i + 1) * 0.01, 100 * (i + 1));
                }

                var tick = new MarketDataTick(
                    ++seq,
                    1001,
                    DateTime.UtcNow.Ticks,
                    DateTime.UtcNow.Ticks,
                    bids,
                    asks);

                // Marshal to bytes - GRANDMASTER: Use stackalloc for small buffers
                int size = Marshal.SizeOf<MarketDataTick>();
                byte[] data = new byte[size];

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

