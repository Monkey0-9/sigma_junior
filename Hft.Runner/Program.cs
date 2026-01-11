using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hft.Core;
using Hft.Core.Audit;
using Hft.Core.RingBuffer; // NEW namespace
using Hft.Feeds;
using Hft.Strategies;
using Hft.Risk;
using Hft.Execution;
using Hft.Infra;

namespace Hft.Runner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting HFT Platform (Institutional Hardened Mode)...");

            System.IO.Directory.CreateDirectory("data/logs");
            var auditLog = new BinaryAuditLog("data/logs", DateTime.UtcNow.ToString("yyyyMMdd"));

            // 1. Setup Core Primitives
            // Using Enterprise RingBuffer (power of 2)
            var tickRing = new LockFreeRingBuffer<MarketDataTick>(1024 * 16);
            var approvedOrderRing = new LockFreeRingBuffer<Order>(1024);
            var preRiskOrderRing = new LockFreeRingBuffer<Order>(1024);
            // ObjectPool removed

            var position = new PositionSnapshot { InstrumentId = 1001 };
            var pnlEngine = new PnlEngine(position);

            // Metrics
            var ticksProcessed = new MetricsCounter("hft_ticks_processed");
            var ordersGenerated = new MetricsCounter("hft_orders_generated");
            var ordersApproved = new MetricsCounter("hft_orders_approved");
            var ordersRejected = new MetricsCounter("hft_orders_rejected");
            var executedOrders = new MetricsCounter("hft_orders_executed");

            var metricsList = new List<MetricsCounter> { ticksProcessed, ordersGenerated, ordersApproved, ordersRejected, executedOrders };

            // 2. Setup Components
            int udpPort = 5001;
            var simulator = new UdpMarketDataSimulator(udpPort, 100);
            var listener = new UdpMarketDataListener(udpPort, tickRing);

            // Strategy
            var strategy = new MarketMakerStrategy(preRiskOrderRing, ordersGenerated, position, spread: 0.1, qty: 10);

            // Risk
            var limits = new RiskLimits
            {
                MaxOrderQty = 100,
                MaxPosition = 500,
                MaxOrdersPerSec = 50,
                MaxNotionalPerOrder = 20000,
                KillSwitchActive = false
            };
            var riskEngine = new PreTradeRiskEngine(preRiskOrderRing, approvedOrderRing, position, ordersApproved, ordersRejected, limits, auditLog);

            // Execution
            var executionStub = new ExecutionStub(approvedOrderRing, pnlEngine, executedOrders);

            // Infra
            var metricsServer = new MetricsServer(9100, metricsList, position);

            // 3. Start everything
            Console.WriteLine("Initializing components...");
            simulator.Start();
            listener.Start();
            riskEngine.Start();
            executionStub.Start();
            metricsServer.Start();

            // Strategy Loop
            var cts = new CancellationTokenSource();

            var strategyTask = Task.Run(() =>
            {
                var spin = new SpinWait();
                while (!cts.IsCancellationRequested)
                {
                    if (tickRing.TryRead(out var tick))
                    {
                        strategy.OnTick(ref tick);
                        ticksProcessed.Increment();
                        pnlEngine.MarkToMarket((tick.BidPrice + tick.AskPrice) / 2.0);
                    }
                    else
                    {
                        spin.SpinOnce();
                    }
                }
            });

            Console.WriteLine("System Running. Press Ctrl+C to stop.");

            var tuiTask = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    Console.Clear();
                    Console.WriteLine($"=== HFT MONITOR [{(DateTime.UtcNow):O}] ===");
                    Console.WriteLine($"Ticks Processed: {ticksProcessed.Get()}");
                    Console.WriteLine($"Orders Gen/Appr/Rej/Exec: {ordersGenerated.Get()} / {ordersApproved.Get()} / {ordersRejected.Get()} / {executedOrders.Get()}");
                    await Task.Delay(1000);
                }
            });

            var tcs = new TaskCompletionSource<bool>();
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; tcs.SetResult(true); cts.Cancel(); };

            await tcs.Task;

            simulator.Stop();
            listener.Stop();
            riskEngine.Stop();
            executionStub.Stop();
            metricsServer.Stop();
            auditLog.Dispose();
        }
    }
}
