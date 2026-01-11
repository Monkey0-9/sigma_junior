using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hft.Core;
using System.Collections.Generic;

namespace Hft.Infra
{
    public class MetricsServer
    {
        private readonly HttpListener _listener;
        private readonly List<MetricsCounter> _counters;
        private readonly PositionSnapshot _position;
        private readonly CancellationTokenSource _cts;
        private Task _task = Task.CompletedTask;

        public MetricsServer(int port, List<MetricsCounter> counters, PositionSnapshot position)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://*:{port}/metrics/");
            _counters = counters;
            _position = position;
            _cts = new CancellationTokenSource();
        }

        public void Start()
        {
            _listener.Start();
            _task = Task.Run(RunLoop);
        }

        public void Stop()
        {
            _cts.Cancel();
            _listener.Stop();
        }

        private async Task RunLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    var sb = new StringBuilder();
                    
                    // Standard counters
                    foreach (var c in _counters)
                    {
                        sb.AppendLine($"# HELP {c.Name} Metric");
                        sb.AppendLine($"# TYPE {c.Name} counter");
                        sb.AppendLine($"{c.Name} {c.Get()}");
                    }
                    
                    // PnL & Position Gauges
                    sb.AppendLine($"hft_position {_position.NetPosition}");
                    sb.AppendLine($"hft_pnl_realized {_position.RealizedPnL}");
                    sb.AppendLine($"hft_pnl_unrealized {_position.UnrealizedPnL}");
                    sb.AppendLine($"hft_avg_entry {_position.AvgEntryPrice}");

                    byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                    context.Response.Close();
                }
                catch (Exception)
                {
                    // Listener stopped or error
                    if (_cts.IsCancellationRequested) break;
                }
            }
        }
    }
}
