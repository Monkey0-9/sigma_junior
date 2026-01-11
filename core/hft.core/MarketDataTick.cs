using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Hft.Core
{
    /// <summary>
    /// Institutional-grade MarketDataTick.
    /// Packed, blittable, and cache-line aware.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct MarketDataTick
    {
        public readonly long SendTimestampTicks;
        public readonly long ReceiveTimestampTicks;

        // Trade Data
        public readonly double Price;
        public readonly int Size;

        // Quote Data (L1)
        public readonly double BidPrice;
        public readonly double AskPrice;
        public readonly int BidSize;
        public readonly int AskSize;

        public readonly int InstrumentId;
        public readonly int SequenceNumber;

        // Alignment to cache line
        private readonly long _pad1, _pad2, _pad3, _pad4;

        public MarketDataTick(
            long sendTimestamp,
            long receiveTimestamp,
            double price,
            int size,
            double bidPrice,
            double askPrice,
            int bidSize,
            int askSize,
            int instrumentId,
            int seq)
        {
            SendTimestampTicks = sendTimestamp;
            ReceiveTimestampTicks = receiveTimestamp;
            Price = price;
            Size = size;
            BidPrice = bidPrice;
            AskPrice = askPrice;
            BidSize = bidSize;
            AskSize = askSize;
            InstrumentId = instrumentId;
            SequenceNumber = seq;
            _pad1 = _pad2 = _pad3 = _pad4 = 0;
        }

        public static MarketDataTick CreateQuote(double bid, double ask, int bSize, int aSize, int instId, int seq)
        {
            long now = Stopwatch.GetTimestamp();
            return new MarketDataTick(now, now, (bid + ask) / 2.0, 0, bid, ask, bSize, aSize, instId, seq);
        }
    }
}
