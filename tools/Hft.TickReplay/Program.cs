using System;
using System.IO;
using System.Runtime.InteropServices;
using Hft.Core;
using Hft.Infra;

namespace Hft.TickReplay
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: TickReplay.exe /date:YYYYMMDD [/instrument:ID]");
                return;
            }

            string date = "";
            long instrumentId = 0;

            foreach (var arg in args)
            {
                if (arg.StartsWith("/date:"))
                {
                    date = arg.Substring(6);
                }
                else if (arg.StartsWith("/instrument:"))
                {
                    long.TryParse(arg.Substring(12), out instrumentId);
                }
            }

            if (string.IsNullOrEmpty(date))
            {
                Console.WriteLine("Error: /date is required.");
                return;
            }

            // Assume standard path
            string auditPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../Hft.Runner/data/audit", $"audit_{date}.bin");
            if (!File.Exists(auditPath))
            {
                // Try relative to execution if built differently
                auditPath = Path.Combine("data/audit", $"audit_{date}.bin");
            }

            if (!File.Exists(auditPath))
            {
                 Console.WriteLine($"Error: Audit log not found at {auditPath}");
                 return;
            }

            Console.WriteLine($"Replaying ticks from {auditPath}...");
            
            // Hardcoded HMAC key for now (same as CompositeEventLogger default)
            byte[] key = System.Text.Encoding.UTF8.GetBytes("98765432101234567890123456789012");

            int count = 0;
            try
            {
                foreach (var record in AppendOnlyLog.Read(auditPath, key))
                {
                    if (record.Type == (byte)AuditRecordType.Tick)
                    {
                        var tick = MemoryMarshal.Read<MarketDataTick>(record.Payload);
                        if (instrumentId == 0 || tick.InstrumentId == instrumentId)
                        {
                            Console.WriteLine($"[{new DateTime(record.Timestamp):HH:mm:ss.fff}] TICK: Inst={tick.InstrumentId} Price={tick.MidPrice:F2}");
                            count++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during replay: {ex.Message}");
            }

            Console.WriteLine($"Replay complete. Processed {count} ticks.");
        }
    }
}
