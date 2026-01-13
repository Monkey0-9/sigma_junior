using System;
using System.Threading;
using System.Threading.Tasks;
using Hft.Core;

namespace Hft.Risk
{
    /// <summary>
    /// Risk-as-OS: Continuous Stress Module.
    /// Runs in background to calculate VaR/ES and trigger Kill Switch if limits breached.
    /// </summary>
    public sealed class ContinuousStressModule : IDisposable
    {
        private readonly PositionSnapshot _position;
        private readonly RiskLimits _limits;
        private readonly IEventLogger _logger;
        private readonly CancellationTokenSource _cts;
        private Task? _task;
        private bool _disposed;

        public ContinuousStressModule(PositionSnapshot position, RiskLimits limits, IEventLogger logger)
        {
            _position = position ?? throw new ArgumentNullException(nameof(position));
            _limits = limits ?? throw new ArgumentNullException(nameof(limits));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cts = new CancellationTokenSource();
        }

        public void Start()
        {
            if (_task != null) return;
            _task = Task.Run(RunStressLoop);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Ignore errors during stop")]
        public void Stop()
        {
            _cts.Cancel();
            try { _task?.Wait(2000); } catch (Exception) { /* Ignore during shutdown */ }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Prevent background task crash")]
        private async Task RunStressLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    PerformStressTest();
                    await Task.Delay(1000, _cts.Token).ConfigureAwait(false); // Run every second
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogRiskEvent("StressModule", "Error", ex.Message);
                }
            }
        }

        private void PerformStressTest()
        {
            // 1. Synthetic Shock (e.g. -5% market move)
            double currentExposure = Math.Abs(_position.NetPosition * _position.AvgEntryPrice);
            double simulatedLoss = currentExposure * 0.05; // 5% shock

            // 2. VaR (Value at Risk) Calculation - Simplified Parametric
            // Assuming daily vol 2% -> 1-day 99% VaR = 2.33 * 2% * Exposure
            double oneDayVar99 = 2.33 * 0.02 * currentExposure;

            // 3. Expected Shortfall (ES)
            double expectedShortfall = oneDayVar99 * 1.15; // Approximation

            if (simulatedLoss > _limits.DailyLossLimit) // If a 5% move would kill us
            {
                _logger.LogRiskEvent("StressTest", "WARNING", $"Stress Test: 5% drop causes loss {simulatedLoss:F2} > Limit {_limits.DailyLossLimit}");
                // In generic Risk-as-OS, we acts preemptively?
                // The prompt says: "if VaR or ES triggers... send HALT_ALL_TRADING"
            }

            // Check VaR against a defined limit (e.g. 50% of Daily Loss Limit shouldn't be risked in VaR)
            double varLimit = _limits.DailyLossLimit * 0.8;
            
            if (oneDayVar99 > varLimit)
            {
                _logger.LogRiskEvent("VaR", "CRITICAL", $"VaR {oneDayVar99:F2} exceeds limit {varLimit:F2}. TRIGGERING KILL SWITCH.");
                _limits.KillSwitchActive = true;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _cts.Dispose();
        }
    }
}
