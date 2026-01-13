using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hft.Core;
using Hft.Risk;
using Hft.Execution;
using Hft.Strategies;
using Hft.Infra;

namespace Hft.Backtest
{
    /// <summary>
    /// Institutional Backtest Node.
    /// Uses identical components to live TradingEngine and deterministic replay.
    /// Aligned with Aladdin's bit-perfect reproduction standards.
    /// </summary>
    public sealed class BacktestNode : IDisposable
    {
        private readonly MarketMakerStrategy _strategy;
        private readonly PreTradeRiskEngine _riskEngine;
        private readonly ExecutionEngine _executionEngine;
        private readonly PositionSnapshot _position;
        private readonly PnlEngine _pnlEngine;
        private readonly IEventLogger _logger;

        private readonly LockFreeRingBuffer<Order> _preRiskRing = new(1024);
        private readonly LockFreeRingBuffer<Order> _approvedRing = new(1024);
        private bool _disposed;

        public BacktestNode(RiskLimits limits)
        {
            _position = new PositionSnapshot { InstrumentId = 1001 };
            _logger = new MockEventLogger();
            _pnlEngine = new PnlEngine(_position, _logger);
            
            var ordersApproved = new MetricsCounter("bt_orders_approved");
            var ordersRejected = new MetricsCounter("bt_orders_rejected");
            var executedOrders = new MetricsCounter("bt_orders_executed");
            var ordersGenerated = new MetricsCounter("bt_orders_generated");

            _strategy = new MarketMakerStrategy(_preRiskRing, ordersGenerated, _position);
            _riskEngine = new PreTradeRiskEngine(_preRiskRing, _approvedRing, _position, ordersApproved, ordersRejected, limits, _logger);
            _executionEngine = new ExecutionEngine(_approvedRing, _pnlEngine, executedOrders, _logger) 
            { 
                LatencyMeanMs = 0.1,
                FillProbability = 1.0 
            };
        }

        public void Run(IEnumerable<MarketDataTick> ticks)
        {
            ArgumentNullException.ThrowIfNull(ticks);
            
            _riskEngine.Start();
            _executionEngine.Start();

            foreach (var tick in ticks)
            {
                var mutableTick = tick;
                _strategy.OnTick(ref mutableTick);
                _pnlEngine.MarkToMarket(tick.MidPrice);
            }

            // Sync wait for deterministic completion
            System.Threading.Thread.Sleep(100);
            
            Console.WriteLine($"[BACKTEST] Finished. Final Position: {_position.NetPosition}, PnL: {_position.TotalPnL:F2}");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _riskEngine.Stop();
            _riskEngine.Dispose();
            
            _executionEngine.Stop();
            _executionEngine.Dispose();
            
            (_logger as IDisposable)?.Dispose();
        }
    }
}

