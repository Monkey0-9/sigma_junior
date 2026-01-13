using System;
using Hft.Core;
using Hft.Core.Audit;

namespace Hft.Infra
{
    /// <summary>
    /// Bridges high-performance binary logging with human-readable structured logging.
    /// Institutional standard for dual-path auditing.
    /// </summary>
    public class CompositeEventLogger : IEventLogger, IDisposable
    {
        private readonly AppendOnlyLog _binaryLog;
        private readonly StructuredEventLogger _structuredLog;
        private bool _disposed;

        public CompositeEventLogger(string directory, string dateStr, byte[]? hmacKey = null)
        {
            hmacKey ??= System.Text.Encoding.UTF8.GetBytes("98765432101234567890123456789012");
            var binaryPath = System.IO.Path.Combine(directory, $"audit_{dateStr}.bin");
            _binaryLog = new AppendOnlyLog(binaryPath, hmacKey);
            _structuredLog = new StructuredEventLogger(directory, dateStr);
        }

        public void LogInfo(string component, string message) => _structuredLog.LogInfo(component, message);
        public void LogWarning(string component, string message) => _structuredLog.LogWarning(component, message);
        public void LogError(string component, string message) => _structuredLog.LogError(component, message);

        public void LogOrder(AuditRecordType type, in Order order)
        {
            _binaryLog.Append((byte)type, in order);
            _structuredLog.LogOrder(type, in order);
        }

        public void LogFill(in Fill fill)
        {
            _binaryLog.Append((byte)AuditRecordType.Fill, in fill);
            _structuredLog.LogFill(in fill);
        }

        public void LogRiskEvent(string rule, string action, string message)
        {
            _structuredLog.LogRiskEvent(rule, action, message);
        }

        public void LogPnlUpdate(long instrumentId, double netPos, double realizedPnl, double unrealizedPnl)
        {
            // Binary logging for PnL could be added if we define a PnlUpdate struct
            _structuredLog.LogPnlUpdate(instrumentId, netPos, realizedPnl, unrealizedPnl);
        }

        public void LogTick(in MarketDataTick tick)
        {
            _binaryLog.Append((byte)AuditRecordType.Tick, in tick);
            // Optionally skip structured log for ticks to save disk, or sample it.
            // _structuredLog.LogInfo("Tick", $"Inst={tick.InstrumentId} P={tick.MidPrice}");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _binaryLog.Dispose();
                    _structuredLog.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}

