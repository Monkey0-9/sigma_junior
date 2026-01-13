using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Hft.Core;
using Hft.Infra;

namespace Hft.Ml.Ingester
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: MlIngester.exe date [output_dir]");
                return;
            }

            string date = args[0];
            string outputDir = args.Length > 1 ? args[1] : "data/training";
            
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            // Standard audit log path
            string auditPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../Hft.Runner/data/audit", $"audit_{date}.bin");
            if (!File.Exists(auditPath))
            {
                auditPath = Path.Combine("data/audit", $"audit_{date}.bin");
            }

            if (!File.Exists(auditPath))
            {
                Console.WriteLine($"Error: Audit log not found at {auditPath}");
                return;
            }

            string outputPath = Path.Combine(outputDir, $"{date}.jsonl");
            Console.WriteLine($"Ingesting {auditPath} to {outputPath}...");

            byte[] key = System.Text.Encoding.UTF8.GetBytes("98765432101234567890123456789012");
            int count = 0;

            using var sw = new StreamWriter(outputPath);

            foreach (var record in AppendOnlyLog.Read(auditPath, key))
            {
                object? data = null;
                string type = "";

                if (record.Type == (byte)AuditRecordType.Tick)
                {
                    data = MemoryMarshal.Read<MarketDataTick>(record.Payload);
                    type = "TICK";
                }
                else if (record.Type == (byte)AuditRecordType.Fill)
                {
                    data = MemoryMarshal.Read<Fill>(record.Payload);
                    type = "FILL";
                }
                else if (record.Type == (byte)AuditRecordType.OrderSubmit || 
                         record.Type == (byte)AuditRecordType.OrderReject || 
                         record.Type == (byte)AuditRecordType.OrderCancel)
                {
                    data = MemoryMarshal.Read<Order>(record.Payload);
                    type = "ORDER";
                }

                if (data != null)
                {
                    var envelope = new
                    {
                        ts = record.Timestamp,
                        cat = type,
                        sub_type = ((AuditRecordType)record.Type).ToString(),
                        payload = data
                    };
                    sw.WriteLine(JsonSerializer.Serialize(envelope));
                    count++;
                }
            }

            Console.WriteLine($"Ingestion complete. Processed {count} records.");
        }
    }
}
