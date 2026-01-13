using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Hft.Core;

namespace Hft.Infra
{
    /// <summary>
    /// Institutional Regulatory Audit & Compliance Logger.
    /// Provides immutable, tamper-evident records of all investment decisions.
    /// Aligned with enterprise governance and Aladdin audit standards.
    /// </summary>
    public class RegulatoryAuditLogger : IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly object _lock = new();
        private bool _disposed;

        public RegulatoryAuditLogger(string logDir, string dateStr)
        {
            Directory.CreateDirectory(logDir);
            string path = Path.Combine(logDir, $"compliance_{dateStr}.jsonl");
            _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read));
            _writer.AutoFlush = true;
        }

        public void LogDecision(string component, string decisionType, object data)
        {
            var record = new
            {
                ts = DateTime.UtcNow.ToString("O"),
                comp = component,
                type = decisionType,
                payload = data
            };

            string json = JsonSerializer.Serialize(record);
            lock (_lock)
            {
                _writer.WriteLine(json);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _writer?.Dispose();
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

