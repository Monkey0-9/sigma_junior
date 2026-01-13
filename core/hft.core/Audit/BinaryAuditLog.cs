using System;
using System.IO;
using System.Text;
using System.Runtime.CompilerServices;

using Hft.Core;

namespace Hft.Core.Audit
{
    public class BinaryAuditLog : IDisposable
    {
        private readonly FileStream _fs;
        private readonly BinaryWriter _writer;
        private readonly object _lock = new object();
        private bool _disposed;

        public BinaryAuditLog(string directory, string dateStr)
        {
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"audit_{dateStr}.bin");
            _fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            _writer = new BinaryWriter(_fs, Encoding.UTF8, leaveOpen: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LogOrder(in Order order, AuditRecordType type)
        {
            lock (_lock)
            {
                // Format: Length(4) + Type(1) + Ticks(8) + Payload
                _writer.Write(1 + 8 + 8 + 8 + 1 + 8 + 8);
                _writer.Write((byte)type);
                _writer.Write(DateTime.UtcNow.Ticks);
                _writer.Write(order.OrderId);
                _writer.Write(order.InstrumentId);
                _writer.Write((byte)order.Side);
                _writer.Write(order.Price);
                _writer.Write(order.Quantity);
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
                        _writer?.Flush();
                        _writer?.Close();
                        _fs?.Close();
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

