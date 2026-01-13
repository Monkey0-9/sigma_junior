using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hft.Core;

namespace Hft.Infra
{
    /// <summary>
    /// Institutional Metrics HTTP Server.
    /// Exposes Prometheus-compatible metrics endpoint for monitoring.
    /// Thread-safe, disposable, production-ready.
    /// GRANDMASTER: Properly handles port binding failures and uses CultureInfo.InvariantCulture.
    /// </summary>
    public sealed class MetricsServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly IReadOnlyList<MetricsCounter> _counters;
        private readonly PositionSnapshot _position;
        private readonly CancellationTokenSource _cts;
        private Task? _task;
        private bool _disposed;

        public bool IsEnabled { get; private set; } = true;

        // GRANDMASTER: Fixed format strings for CA1863 compliance
        private readonly string MetricsStartedFormat = "[METRICS] Listening on {0}";
        private readonly string MetricsWarningFormat = "[WARNING] Failed to start MetricsServer: {0}";

        // GRANDMASTER: CA1303 compliance - use const strings with SuppressMessage attributes
        // Justification: Console output messages are operational logs, not user-facing UI strings,
        // and don't require localization in this institutional trading system context.
        private const string MetricsPermissionWarning = "[WARNING] Metrics server requires elevated permissions or URL reservation.";
        private const string MetricsEnableInfo = "[INFO] To enable metrics, run: netsh http add urlacl url=http://+:{0}/ user=%USERDOMAIN%\\%USERNAME%";
        private const string MetricsContinueWarning = "[WARNING] System will continue without external metrics extraction.";

        public MetricsServer(int port, IReadOnlyList<MetricsCounter> counters, PositionSnapshot position)
        {
            _listener = new HttpListener();
            // Institutional DEFAULT: Bind to 127.0.0.1 only (least privilege)
            // Use unprivileged port (default 9180) to avoid Admin requirements
            _listener.Prefixes.Add(string.Format(CultureInfo.InvariantCulture, "http://127.0.0.1:{0}/metrics/", port));
            _counters = counters;
            _position = position;
            _cts = new CancellationTokenSource();
        }

        [SuppressMessage("Major Code Smell", "S4834:Closing a stream might leave it non-reusable. If multiple threads access a stream, even synchronously, closing the stream may cause undefined behavior, such as ObjectDisposedException.", Justification = "HttpListener is safe to close in this context")]
        [SuppressMessage("Usage", "CA1303:Do not pass literals as localized parameters", Justification = "Console logs are operational, not user-facing")]
        public void Start()
        {
            try
            {
                _listener.Start();
                _task = Task.Run(RunLoop);
                Console.WriteLine(
                    string.Format(CultureInfo.InvariantCulture, MetricsStartedFormat,
                        string.Join(", ", _listener.Prefixes)));
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 5) // Access denied
            {
                IsEnabled = false;
                Console.WriteLine(
                    string.Format(CultureInfo.InvariantCulture, MetricsWarningFormat, ex.Message));
                Console.WriteLine(MetricsPermissionWarning);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, MetricsEnableInfo, GetPortFromPrefix()));
                Console.WriteLine(MetricsContinueWarning);
            }
            catch (HttpListenerException ex)
            {
                IsEnabled = false;
                Console.WriteLine(
                    string.Format(CultureInfo.InvariantCulture, MetricsWarningFormat, ex.Message));
                Console.WriteLine(MetricsContinueWarning);
            }
            catch (InvalidOperationException ex)
            {
                IsEnabled = false;
                Console.WriteLine(
                    string.Format(CultureInfo.InvariantCulture, MetricsWarningFormat, ex.Message));
                Console.WriteLine(MetricsContinueWarning);
            }
        }

        private int GetPortFromPrefix()
        {
            foreach (var prefix in _listener.Prefixes)
            {
                // Extract port from "http://127.0.0.1:9180/metrics/"
                var parts = prefix.Split(':');
                if (parts.Length >= 3 && int.TryParse(parts[2], out int port))
                {
                    return port;
                }
            }
            return 9180; // Default
        }

        public void Stop()
        {
            if (!IsEnabled) return;
            _cts.Cancel();
            try
            {
                if (_listener.IsListening)
                {
                    _listener.Stop();
                }
            }
            catch (ObjectDisposedException)
            {
                // Listener already disposed
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Stop();
            _cts.Dispose();
            ((IDisposable)_listener).Dispose();
            GC.SuppressFinalize(this);
        }

        [SuppressMessage("Usage", "CA1303:Do not pass literals as localized parameters", Justification = "Console logs are operational, not user-facing")]
        private async Task RunLoop()
        {
            var invariant = CultureInfo.InvariantCulture;

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync().ConfigureAwait(false);
                    var sb = new StringBuilder();

                    // GRANDMASTER: Fetch metrics from CentralMetricsStore
                    foreach (var m in CentralMetricsStore.Instance.GetAllMetrics())
                    {
                        var typeStr = m.Type == MetricType.Counter ? "counter" : "gauge";
                        sb.AppendLine(string.Format(invariant, "# HELP {0} Metric", m.Name));
                        sb.AppendLine(string.Format(invariant, "# TYPE {0} {1}", m.Name, typeStr));
                        sb.AppendLine(string.Format(invariant, "{0} {1}", m.Name, m.Value));
                    }

                    // PnL & Position Gauges
                    sb.AppendLine(string.Format(invariant, "hft_position {0}", _position.NetPosition));
                    sb.AppendLine(string.Format(invariant, "hft_pnl_realized {0}", _position.RealizedPnL));
                    sb.AppendLine(string.Format(invariant, "hft_pnl_unrealized {0}", _position.UnrealizedPnL));
                    sb.AppendLine(string.Format(invariant, "hft_avg_entry {0}", _position.AvgEntryPrice));

                    byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                    context.Response.ContentLength64 = bytes.Length;
                    // GRANDMASTER: Use ReadOnlyMemory<byte> overload for proper async I/O
                    await context.Response.OutputStream.WriteAsync(new ReadOnlyMemory<byte>(bytes), _cts.Token).ConfigureAwait(false);
                    context.Response.Close();
                }
                catch (ObjectDisposedException)
                {
                    if (_cts.IsCancellationRequested) break;
                }
                catch (HttpListenerException)
                {
                    if (_cts.IsCancellationRequested) break;
                }
            }
        }
    }
}

