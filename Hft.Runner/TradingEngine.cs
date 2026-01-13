using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hft.Core;
using Hft.Execution;
using Hft.Feeds;
using Hft.Infra;
using Hft.Risk;
using Hft.Strategies;
using Hft.Routing;

namespace Hft.Runner
{
    /// <summary>
    /// Institutional-grade orchestrator for the HFT node.
    /// Designed to meet ALADDIN / Jane Street operational standards.
    /// </summary>
    internal sealed class TradingEngine : IDisposable
    {
        private EngineState _state = EngineState.Init;
        private readonly CancellationTokenSource _cts = new();

        private readonly UdpMarketDataSimulator _simulator;
        private readonly UdpMarketDataListener _listener;
        private readonly MarketMakerStrategy _strategy;
        private readonly PreTradeRiskEngine _riskEngine;
        private readonly ExecutionEngine _executionEngine;
        private readonly MetricsServer _metricsServer;
        private readonly CompositeEventLogger _logger;
        private readonly LatencyMonitor _latencyMonitor;

        private readonly LockFreeRingBuffer<MarketDataTick> _tickRing;
        private readonly PositionSnapshot _position;
        private readonly PnlEngine _pnlEngine;
        private readonly ContinuousStressModule _stressModule;
        private readonly PolicyEnforcementMiddleware _governanceMiddleware;
        private readonly CentralMetricsStore _metricsStore = CentralMetricsStore.Instance;
        private readonly HealthMonitor _healthMonitor;
        private readonly Hft.Routing.SmartOrderRouter _sor;
        private readonly TradingConfig _config;

        internal class TradingConfig
        {
            public int UdpPort { get; set; }
            public int MetricsPort { get; set; }
            public RiskLimits Risk { get; set; } = new();
            public ExecutionParams Execution { get; set; } = new();
        }

        internal class ExecutionParams
        {
            public string Strategy { get; set; } = "POV";
            public double RiskAversion { get; set; } = 1e-6;
            public double DailyVolatility { get; set; } = 0.02;
            public double TemporaryImpact { get; set; } = 0.1;
            public double PermanentImpact { get; set; } = 0.05;
        }

        private readonly string _pidPath = ".runner.pid";
        private bool _disposed;

        public EngineState State => _state;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1303:Do not pass literals as localized parameters", Justification = "Operational logs")]
        public TradingEngine(int udpPort, int metricsPort)
        {
            var invariant = CultureInfo.InvariantCulture;

            Console.WriteLine(string.Format(
                invariant,
                Strings.EngineInit,
                DateTime.UtcNow));

            FilesystemBootstrap.EnsureDirectories(AppDomain.CurrentDomain.BaseDirectory);
            File.WriteAllText(_pidPath, Environment.ProcessId.ToString(invariant));

            // Load Configuration
            _config = LoadConfig() ?? new TradingConfig 
            { 
                UdpPort = udpPort, 
                MetricsPort = metricsPort 
            };

            _logger = new CompositeEventLogger("data/audit", DateTime.UtcNow.ToString("yyyyMMdd", invariant));

            _tickRing = new LockFreeRingBuffer<MarketDataTick>(16384);
            var preRiskOrders = new LockFreeRingBuffer<Order>(1024);
            var approvedOrders = new LockFreeRingBuffer<Order>(1024);

            _position = new PositionSnapshot { InstrumentId = 1001 };
            _pnlEngine = new PnlEngine(_position, _logger);

            // Initialize Observability
            _healthMonitor = new HealthMonitor(_metricsStore, _logger);
            
            // Initialize Execution & Routing
            _sor = new Hft.Routing.SmartOrderRouter(
                new Hft.Routing.RouterConfig { MaxOrdersPerSecond = _config.Risk.MaxOrdersPerSec },
                null,
                new Hft.Routing.NullPreTradeRiskEngine());

            _simulator = new UdpMarketDataSimulator(_config.UdpPort, 100);
            _listener = new UdpMarketDataListener(_config.UdpPort, _tickRing);

            // GOVERNANCE KERNEL CHECKS
            _governanceMiddleware = new PolicyEnforcementMiddleware("http://localhost:5000");
            _governanceMiddleware.CheckAuthorization("MarketMakerStrategy");

            var qualityManager = new SignalQualityManager(_logger);
            _strategy = new MarketMakerStrategy(preRiskOrders, _metricsStore.GetCounter("hft_orders_total"), _position, qualityManager, 0.1, 10);

            _riskEngine = new PreTradeRiskEngine(
                preRiskOrders,
                approvedOrders,
                _position,
                _metricsStore.GetCounter("hft_orders_approved_total"),
                _metricsStore.GetCounter("hft_orders_rejected_total"),
                _config.Risk,
                _logger);

            _executionEngine = new ExecutionEngine(
                approvedOrders,
                _pnlEngine,
                _metricsStore.GetCounter("hft_orders_executed_total"),
                _logger);

            _metricsServer = new MetricsServer(_config.MetricsPort, Array.Empty<MetricsCounter>(), _position);
            _latencyMonitor = new LatencyMonitor();
            _stressModule = new ContinuousStressModule(_position, _config.Risk, _logger);

            _state = EngineState.Standby;

            Console.WriteLine(string.Format(
                invariant,
                Strings.EngineStandby,
                DateTime.UtcNow));
        }

        private static TradingConfig? LoadConfig()
        {
            const string ConfigPath = "config.json";
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigPath);
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    return System.Text.Json.JsonSerializer.Deserialize<TradingConfig>(json);
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[Config] IO error loading config: {ex.Message}");
            }
            catch (System.Text.Json.JsonException ex)
            {
                Console.WriteLine($"[Config] JSON error loading config: {ex.Message}");
            }
            return null;
        }

        public async Task StartAsync(CancellationToken externalToken)
        {
            if (_state != EngineState.Standby)
                return;

            _simulator.Start();
            _listener.Start();
            _riskEngine.Start();
            _executionEngine.Start();
            _metricsServer.Start();
            _stressModule.Start();
            _healthMonitor.Start();

            _state = EngineState.Trading;

            using var linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(externalToken, _cts.Token);

            var strategyTask = Task.Run(
                () => RunStrategyLoop(linkedCts.Token),
                linkedCts.Token);

            var monitorTask = Task.Run(
                () => RunMonitorLoop(linkedCts.Token),
                linkedCts.Token);

            await Task.WhenAny(strategyTask, monitorTask)
                    .ConfigureAwait(false);
        }

        private async Task RunStrategyLoop(CancellationToken token)
        {
            var foundationModel = new Hft.FIOS.MarketFoundationModel();
            var trustRoot = new Hft.FIOS.SovereignTrustRoot();

            trustRoot.RegisterPolicy(new Hft.FIOS.StabilityPolicy());

            while (!token.IsCancellationRequested)
            {
                if (_tickRing.TryRead(out var tick))
                {
                    double simulatedPrice =
                        foundationModel.SimulateDiffusion(tick.MidPrice, 0.02);

                    double allocation =
                        Hft.FIOS.OptimalControlAllocator.ComputeAllocation(0.85, 0, 100_000);

                    var decision = new Hft.FIOS.DecisionRequest
                    {
                        Action = "PROPOSE_ORDER",
                        Context = new Hft.FIOS.SystemState
                        {
                            Leverage = 1.2,
                            Volatility = 0.02,
                            MaxDrawdown = 0.05
                        },
                        Timestamp = DateTime.UtcNow.Ticks
                    };

                    var proof = trustRoot.VerifyDecision(decision);

                    if (proof.IsApproved)
                    {
                        Console.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            Strings.FiosApproval,
                            allocation));

                        _logger.LogTick(in tick);
                        _strategy.OnTick(ref tick);
                        _metricsStore.GetCounter("hft_ticks_total").Increment();
                        _pnlEngine.MarkToMarket(simulatedPrice);
                        
                        long latency = DateTime.UtcNow.Ticks - tick.ReceiveTimestampTicks;
                        _latencyMonitor.Record(latency);
                        _metricsStore.RecordLatency("routing_latency", latency / 10.0); // ticks to us
                    }
                    else
                    {
                        Console.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            Strings.FiosRejection,
                            proof.Reason,
                            proof.Constraint));
                    }
                }

                await Task.Delay(100, token)
                          .ConfigureAwait(false);
            }
        }

        private async Task RunMonitorLoop(CancellationToken token)
        {
            var invariant = CultureInfo.InvariantCulture;

            while (!token.IsCancellationRequested)
            {
                Console.Clear();

                Console.WriteLine(string.Format(
                    invariant,
                    Strings.MonitorHeader,
                    DateTime.UtcNow));

                Console.WriteLine(string.Format(
                    invariant,
                    Strings.MonitorState,
                    _state,
                    Environment.ProcessId));

                foreach (var metric in _metricsStore.GetAllMetrics())
                {
                    Console.WriteLine(string.Format(
                        invariant,
                        "{0}: {1}",
                        metric.Name,
                        metric.Value));
                }

                Console.WriteLine(string.Format(
                    invariant,
                    Strings.MonitorPnl,
                    _position.TotalPnL,
                    _latencyMonitor.GetStats().P99));

                try
                {
                    await Task.Delay(1000, token)
                            .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public void Stop()
        {
            if (_disposed || _state is EngineState.Stopped or EngineState.Stopping)
                return;

            _state = EngineState.Stopping;
            _cts.Cancel();

            _simulator.Stop();
            _listener.Stop();
            _riskEngine.Stop();
            _executionEngine.Stop();
            _stressModule.Stop();
            _healthMonitor.Stop();

            _simulator.Dispose();
            _listener.Dispose();
            _riskEngine.Dispose();
            _executionEngine.Dispose();
            _sor.Dispose();
            _healthMonitor.Dispose();
            _metricsServer.Dispose();
            _stressModule.Dispose();
            _governanceMiddleware?.Dispose();
            _logger.Dispose();

            if (File.Exists(_pidPath))
                File.Delete(_pidPath);

            _state = EngineState.Stopped;

            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                Strings.EngineStopped,
                DateTime.UtcNow));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Stop();
            _cts.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
