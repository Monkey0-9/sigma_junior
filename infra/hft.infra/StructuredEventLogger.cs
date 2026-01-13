using System;
using System.IO;
using System.Text.Json;
using System.Text;
using Hft.Core;
using Hft.Core.Audit;

namespace Hft.Infra
{
    public class StructuredEventLogger : IEventLogger, IDisposable
    {
        private readonly FileStream _fs;
        private readonly StreamWriter _sw;
        private readonly object _lock = new object();
        private bool _disposed;

        public StructuredEventLogger(string directory, string dateStr)
        {
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"events_{dateStr}.jsonl");
            _fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            _sw = new StreamWriter(_fs, Encoding.UTF8) { AutoFlush = true };
        }

        public void LogInfo(string component, string message) => Log("INFO", component, message);
        public void LogWarning(string component, string message) => Log("WARN", component, message);
        public void LogError(string component, string message) => Log("ERROR", component, message);

        public void LogOrder(AuditRecordType type, in Order order)
        {
            var evt = new
            {
                ts = DateTime.UtcNow.ToString("o"),
                type = type.ToString(),
                orderId = order.OrderId,
                instId = order.InstrumentId,
                side = order.Side.ToString(),
                px = order.Price,
                qty = order.Quantity
            };
            WriteJson("ORDER", evt);
        }

        public void LogFill(in Fill fill)
        {
            var evt = new
            {
                ts = DateTime.UtcNow.ToString("o"),
                fillId = fill.FillId,
                orderId = fill.OrderId,
                instId = fill.InstrumentId,
                side = fill.Side.ToString(),
                px = fill.Price,
                qty = fill.Quantity
            };
            WriteJson("FILL", evt);
        }

        public void LogRiskEvent(string rule, string action, string message)
        {
            var evt = new
            {
                ts = DateTime.UtcNow.ToString("o"),
                rule,
                action,
                msg = message
            };
            WriteJson("RISK", evt);
        }

        public void LogPnlUpdate(long instrumentId, double netPos, double realizedPnl, double unrealizedPnl)
        {
            var evt = new
            {
                ts = DateTime.UtcNow.ToString("o"),
                instId = instrumentId,
                pos = netPos,
                realPnL = realizedPnl,
                unrealPnL = unrealizedPnl
            };
            WriteJson("PNL", evt);
        }

        public void LogTick(in MarketDataTick tick)
        {
            // Sample: 1% of ticks or based on vol?
            // For now, allow all but check perf later.
            // Or just minimal JSON.
            // "T" category.
            // NOTE: Logging every tick to JSON might kill IO. 
            // CompositeEventLogger comments it out by default.
            // But we must implement the interface.
        }

        private void Log(string level, string component, string message)
        {
            var evt = new
            {
                ts = DateTime.UtcNow.ToString("o"),
                level,
                comp = component,
                msg = message
            };
            WriteJson("SYSTEM", evt);
        }

        private void WriteJson(string category, object data)
        {
            var wrapper = new { cat = category, data };
            var json = JsonSerializer.Serialize(wrapper);
            lock (_lock)
            {
                _sw.WriteLine(json);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    lock (_lock)
                    {
                        _sw?.Dispose();
                        _fs?.Dispose();
                    }
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

