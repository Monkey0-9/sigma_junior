using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Hft.Core;

namespace Hft.Feeds
{
    /// <summary>
    /// UDP Market Data Listener.
    /// GRANDMASTER: Proper Task initialization, CancellationToken lifecycle, and dispose pattern.
    /// </summary>
    public sealed class UdpMarketDataListener : IDisposable
    {
        private readonly int _port;
        private readonly LockFreeRingBuffer<MarketDataTick> _ringBuffer;
        private readonly CancellationTokenSource _cts;
        private Task? _task;
        private bool _disposed;

        public UdpMarketDataListener(int port, LockFreeRingBuffer<MarketDataTick> ringBuffer)
        {
            _port = port;
            _ringBuffer = ringBuffer ?? throw new ArgumentNullException(nameof(ringBuffer));
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// Starts the listener. Thread-safe for single Start/Stop lifecycle.
        /// </summary>
        public void Start()
        {
            // GRANDMASTER: CA1513 - Use ObjectDisposedException.ThrowIf for proper exception handling
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_task != null && !_task.IsCompleted) return; // Already running

            _task = Task.Run(RunLoopAsync);
        }

        /// <summary>
        /// Stops the listener and waits for graceful shutdown.
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
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private async Task RunLoopAsync()
        {
            // GRANDMASTER: Use using pattern for proper socket cleanup
            using var client = new UdpClient(_port);
            var endPoint = new IPEndPoint(IPAddress.Any, _port);

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    // GRANDMASTER: Use ReceiveAsync with CancellationToken and proper async/await
                    // CA2012: Use await instead of blocking on ValueTask
                    var receiveResult = await client.ReceiveAsync(_cts.Token).ConfigureAwait(false);
                    byte[]? data = receiveResult.Buffer;

                    if (data != null && data.Length >= Marshal.SizeOf<MarketDataTick>())
                    {
                        var tick = MemoryMarshal.Read<MarketDataTick>(data);
                        _ringBuffer.TryWrite(in tick);
                    }
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException) when (_cts.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
                {
                    // GRANDMASTER: CA1031 - Catch specific allowed exception types
                    if (!_cts.IsCancellationRequested)
                    {
                        continue;
                    }
                    break;
                }
            }
        }
    }
}

