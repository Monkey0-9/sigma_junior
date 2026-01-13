using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hft.Core;

namespace Hft.Infra
{
    /// <summary>
    /// Institutional Exporter for ClickHouse.
    /// Periodically flushes metrics and log events to ClickHouse using the HTTP interface.
    /// ClickHouse is preferred for HFT for its high-performance OLAP capabilities.
    /// </summary>
    public sealed class ClickHouseExporter : IDisposable
    {
        private readonly Uri _endpoint;
        private readonly HttpClient _httpClient;
        private readonly CancellationTokenSource _cts = new();
        private Task? _flushTask;
        private bool _disposed;

        public ClickHouseExporter(string host, int port = 8123, string database = "hft")
        {
            _endpoint = new Uri($"http://{host}:{port}/?query=INSERT INTO {database}.metrics VALUES");
            _httpClient = new HttpClient();
        }

        public void Start()
        {
            _flushTask = Task.Run(FlushLoop);
        }

        private async Task FlushLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(10000, _cts.Token).ConfigureAwait(false); // Flush every 10s
                    await FlushMetricsAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"[ERROR] ClickHouse connectivity error: {ex.Message}");
                }
                catch (Exception ex) when (ex is not StackOverflowException && ex is not OutOfMemoryException)
                {
                    Console.WriteLine($"[ERROR] ClickHouse unexpected flush failure: {ex.Message}");
                }
            }
        }

        private async Task FlushMetricsAsync()
        {
            var sb = new StringBuilder();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            foreach (var m in CentralMetricsStore.Instance.GetAllMetrics())
            {
                // Format: (timestamp, name, value)
                sb.Append('(').Append(timestamp).Append(",'")
                  .Append(m.Name).Append("',")
                  .Append(m.Value.ToString("F6", System.Globalization.CultureInfo.InvariantCulture))
                  .AppendLine("),");
            }

            if (sb.Length == 0) return;

            // Remove trailing comma and newline
            sb.Length -= 2;

            using var content = new StringContent(sb.ToString(), Encoding.UTF8, "text/plain");
            var response = await _httpClient.PostAsync(_endpoint, content).ConfigureAwait(false);
            
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Console.WriteLine($"[ERROR] ClickHouse rejection: {err}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _httpClient.Dispose();
            _cts.Dispose();
        }
    }
}
