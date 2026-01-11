using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Hft.Core;
using Hft.Strategies;

namespace Hft.Backtest
{
    public class TickReplay
    {
        private readonly string _filePath;
        private readonly IStrategy _strategy;

        public TickReplay(string filePath, IStrategy strategy)
        {
            _filePath = filePath;
            _strategy = strategy;
        }

        public void Run()
        {
             // Simple CSV format assumption: Timestamp,InstrumentId,BidPrice,AskPrice,BidSize,AskSize
             // Skipping header check for brevity in demo
             foreach (var line in File.ReadLines(_filePath))
             {
                 if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;
                 
                 var parts = line.Split(',');
                 if (parts.Length < 6) continue;

                 // Use mid price for the simplified MarketDataTick
                 double bidPrice = double.Parse(parts[2], CultureInfo.InvariantCulture);
                 double askPrice = double.Parse(parts[3], CultureInfo.InvariantCulture);
                 double midPrice = (bidPrice + askPrice) / 2.0;
                 int size = (int)double.Parse(parts[4], CultureInfo.InvariantCulture);

                 var tick = new MarketDataTick(
                     sendTimestampTicks: long.Parse(parts[0]),
                     receiveTimestampTicks: DateTime.UtcNow.Ticks,
                     price: midPrice,
                     size: size
                 );
                 
                 _strategy.OnTick(ref tick);
             }
        }
    }
}
