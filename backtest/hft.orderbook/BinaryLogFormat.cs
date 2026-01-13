using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Hft.OrderBook
{
    public enum LogEventType
    {
        None = 0,
        OrderAdd = 1,
        OrderCancel = 2,
        OrderAmend = 3,
        Trade = 4,
        MarketData = 5,
        BboChange = 6,
        Heartbeat = 7,
        Snapshot = 8,
        OrderReject = 9,
        Custom = 31
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct LogHeader : IEquatable<LogHeader>
    {
        public uint Magic;
        public ushort Version;
        public ushort Flags;
        public long InstrumentId;
        public long StartTimestamp;
        public long EndTimestamp;
        public long EventCount;
        public long FileSize;
        public fixed byte Reserved[20];

        public override bool Equals(object? obj) => obj is LogHeader other && Equals(other);
        public bool Equals(LogHeader other) => Magic == other.Magic && InstrumentId == other.InstrumentId && StartTimestamp == other.StartTimestamp;
        public override int GetHashCode() => HashCode.Combine(Magic, InstrumentId, StartTimestamp);
        public static bool operator ==(LogHeader left, LogHeader right) => left.Equals(right);
        public static bool operator !=(LogHeader left, LogHeader right) => !left.Equals(right);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LogFooter : IEquatable<LogFooter>
    {
        public long EventCount;
        public long FirstSequence;
        public long LastSequence;
        public ulong Checksum;

        public override bool Equals(object? obj) => obj is LogFooter other && Equals(other);
        public bool Equals(LogFooter other) => EventCount == other.EventCount && LastSequence == other.LastSequence;
        public override int GetHashCode() => HashCode.Combine(EventCount, LastSequence);
        public static bool operator ==(LogFooter left, LogFooter right) => left.Equals(right);
        public static bool operator !=(LogFooter left, LogFooter right) => !left.Equals(right);
    }

    public readonly struct EventDescriptor : IEquatable<EventDescriptor>
    {
        public long Position { get; }
        public int Size { get; }
        public OrderEventType Type { get; }
        public long Timestamp { get; }

        public EventDescriptor(long position, int size, OrderEventType type, long timestamp)
        {
            Position = position;
            Size = size;
            Type = type;
            Timestamp = timestamp;
        }

        public override bool Equals(object? obj) => obj is EventDescriptor other && Equals(other);
        public bool Equals(EventDescriptor other) => Position == other.Position && Size == other.Size;
        public override int GetHashCode() => HashCode.Combine(Position, Size);
        public static bool operator ==(EventDescriptor left, EventDescriptor right) => left.Equals(right);
        public static bool operator !=(EventDescriptor left, EventDescriptor right) => !left.Equals(right);
    }

    public sealed class BinaryLogFormat
    {
        public const uint MagicNumber = 0x4F424F4B; // "OBOOK" in ASCII
        public const int CurrentVersion = 2;
        public const int HeaderSize = 64;
        public const int FooterSize = 32;
        public const int MaxEventSize = 256;
        internal const byte EventTypeMask = 0x1F;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SerializeEvent(Span<byte> buffer, AuditEvent evt)
        {
            if (buffer.Length < 9)
                throw new ArgumentException("Buffer too small", nameof(buffer));

            int pos = 0;
            buffer[pos++] = (byte)evt.EventType;
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Timestamp);

            switch (evt.EventType)
            {
                case OrderEventType.Add:
                    WriteAddEvent(buffer, ref pos, evt);
                    break;
                case OrderEventType.Cancel:
                    WriteCancelEvent(buffer, ref pos, evt);
                    break;
                case OrderEventType.Amend:
                    WriteAmendEvent(buffer, ref pos, evt);
                    break;
                case OrderEventType.Fill:
                case OrderEventType.Trade:
                    WriteTradeEvent(buffer, ref pos, evt);
                    break;
                case OrderEventType.BboChange:
                    WriteBboEvent(buffer, ref pos, evt);
                    break;
                case OrderEventType.Reject:
                    WriteRejectEvent(buffer, ref pos, evt);
                    break;
                default:
                    WriteGenericEvent(buffer, ref pos, evt);
                    break;
            }

            return pos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DeserializeEvent(ReadOnlySpan<byte> buffer, out AuditEvent evt)
        {
            if (buffer.Length < 9)
                throw new ArgumentException("Buffer too small", nameof(buffer));

            int pos = 0;
            byte flags = buffer[pos++];
            OrderEventType eventType = (OrderEventType)(flags & EventTypeMask);
            long timestamp = BitConverterHelper.ReadInt64(buffer, ref pos);

            evt = eventType switch
            {
                OrderEventType.Add => ReadAddEvent(buffer, ref pos, timestamp),
                OrderEventType.Cancel => ReadCancelEvent(buffer, ref pos, timestamp),
                OrderEventType.Amend => ReadAmendEvent(buffer, ref pos, timestamp),
                OrderEventType.Fill => ReadFillEvent(buffer, ref pos, timestamp),
                OrderEventType.Trade => ReadTradeEvent(buffer, ref pos, timestamp),
                OrderEventType.BboChange => ReadBboEvent(buffer, ref pos, timestamp),
                OrderEventType.Reject => ReadRejectEvent(buffer, ref pos, timestamp),
                _ => ReadGenericEvent(buffer, ref pos, timestamp, eventType)
            };

            return pos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteAddEvent(Span<byte> buffer, ref int pos, AuditEvent evt)
        {
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.OrderId);
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data1);
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data2);
            BitConverterHelper.WriteInt16(buffer, ref pos, (short)evt.Data3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AuditEvent ReadAddEvent(ReadOnlySpan<byte> buffer, ref int pos, long timestamp)
        {
            long orderId = BitConverterHelper.ReadInt64(buffer, ref pos);
            long price = BitConverterHelper.ReadInt64(buffer, ref pos);
            long qty = BitConverterHelper.ReadInt32(buffer, ref pos);
            long attrs = BitConverterHelper.ReadInt16(buffer, ref pos);

            OrderSide side = (OrderSide)((attrs >> 8) & 0xFF);
            OrderType type = (OrderType)(attrs & 0xFF);
            OrderAttributes flags = (OrderAttributes)((attrs >> 16) & 0xFF);

            return AuditEvent.CreateAddEvent(0, timestamp, orderId, 0, price, (int)qty, side, type, flags, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteCancelEvent(Span<byte> buffer, ref int pos, AuditEvent evt)
        {
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.OrderId);
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data1);
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AuditEvent ReadCancelEvent(ReadOnlySpan<byte> buffer, ref int pos, long timestamp)
        {
            long orderId = BitConverterHelper.ReadInt64(buffer, ref pos);
            long leaves = BitConverterHelper.ReadInt32(buffer, ref pos);
            long original = BitConverterHelper.ReadInt32(buffer, ref pos);
            return AuditEvent.CreateCancelEvent(0, timestamp, orderId, 0, (int)leaves, (int)original);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteAmendEvent(Span<byte> buffer, ref int pos, AuditEvent evt)
        {
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.OrderId);
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data1);
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data2);
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data3);
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AuditEvent ReadAmendEvent(ReadOnlySpan<byte> buffer, ref int pos, long timestamp)
        {
            long orderId = BitConverterHelper.ReadInt64(buffer, ref pos);
            long newQty = BitConverterHelper.ReadInt32(buffer, ref pos);
            long oldQty = BitConverterHelper.ReadInt32(buffer, ref pos);
            long newPrice = BitConverterHelper.ReadInt64(buffer, ref pos);
            long oldPrice = BitConverterHelper.ReadInt64(buffer, ref pos);
            return AuditEvent.CreateAmendEvent(0, timestamp, orderId, 0, (int)newQty, (int)oldQty, newPrice, oldPrice);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteTradeEvent(Span<byte> buffer, ref int pos, AuditEvent evt)
        {
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data1);
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data2);
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data3);
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AuditEvent ReadTradeEvent(ReadOnlySpan<byte> buffer, ref int pos, long timestamp)
        {
            long price = BitConverterHelper.ReadInt64(buffer, ref pos);
            long qty = BitConverterHelper.ReadInt32(buffer, ref pos);
            long buyOrder = BitConverterHelper.ReadInt64(buffer, ref pos);
            long sellOrder = BitConverterHelper.ReadInt64(buffer, ref pos);
            return AuditEvent.CreateTradeEvent(0, timestamp, 0, price, (int)qty, buyOrder, sellOrder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AuditEvent ReadFillEvent(ReadOnlySpan<byte> buffer, ref int pos, long timestamp) => ReadTradeEvent(buffer, ref pos, timestamp);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteBboEvent(Span<byte> buffer, ref int pos, AuditEvent evt)
        {
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data1);
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data2);
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data3);
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AuditEvent ReadBboEvent(ReadOnlySpan<byte> buffer, ref int pos, long timestamp)
        {
            long bidPrice = BitConverterHelper.ReadInt64(buffer, ref pos);
            long bidSize = BitConverterHelper.ReadInt32(buffer, ref pos);
            long askPrice = BitConverterHelper.ReadInt64(buffer, ref pos);
            long askSize = BitConverterHelper.ReadInt32(buffer, ref pos);
            return AuditEvent.CreateBboChangeEvent(0, timestamp, 0, bidPrice, (int)bidSize, askPrice, (int)askSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteRejectEvent(Span<byte> buffer, ref int pos, AuditEvent evt)
        {
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.OrderId);
            buffer[pos++] = (byte)evt.Data1;
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data2);
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AuditEvent ReadRejectEvent(ReadOnlySpan<byte> buffer, ref int pos, long timestamp)
        {
            long orderId = BitConverterHelper.ReadInt64(buffer, ref pos);
            long reason = buffer[pos++];
            long qty = BitConverterHelper.ReadInt32(buffer, ref pos);
            long price = BitConverterHelper.ReadInt64(buffer, ref pos);
            return AuditEvent.CreateRejectEvent(0, timestamp, orderId, 0, (RejectReason)reason, (int)qty, price);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteGenericEvent(Span<byte> buffer, ref int pos, AuditEvent evt)
        {
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.OrderId);
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data1);
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AuditEvent ReadGenericEvent(ReadOnlySpan<byte> buffer, ref int pos, long timestamp, OrderEventType type)
        {
            long orderId = BitConverterHelper.ReadInt64(buffer, ref pos);
            long data1 = BitConverterHelper.ReadInt64(buffer, ref pos);
            long data2 = BitConverterHelper.ReadInt64(buffer, ref pos);
            return new AuditEvent(type, 0, timestamp, orderId, 0, data1, data2, 0, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetEventSize(OrderEventType type)
        {
            return type switch
            {
                OrderEventType.Add => 23,
                OrderEventType.Cancel => 25,
                OrderEventType.Amend => 41,
                OrderEventType.Fill or OrderEventType.Trade => 37,
                OrderEventType.BboChange => 33,
                OrderEventType.Reject => 30,
                _ => 25
            };
        }

        public static ulong CalculateChecksum(ReadOnlySpan<byte> data)
        {
            uint a = 0xFFFF;
            uint d = 0xFFFF;
            for (int i = 0; i < data.Length; i++)
            {
                a = (a + data[i]) % 0xFFF1;
                d = (d + a) % 0xFFF1;
            }
            return ((ulong)d << 16) | a;
        }
    }

    internal static class BitConverterHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt64(Span<byte> buffer, ref int pos, long value)
        {
            buffer[pos++] = (byte)value;
            buffer[pos++] = (byte)(value >> 8);
            buffer[pos++] = (byte)(value >> 16);
            buffer[pos++] = (byte)(value >> 24);
            buffer[pos++] = (byte)(value >> 32);
            buffer[pos++] = (byte)(value >> 40);
            buffer[pos++] = (byte)(value >> 48);
            buffer[pos++] = (byte)(value >> 56);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadInt64(ReadOnlySpan<byte> buffer, ref int pos)
        {
            long value = buffer[pos++];
            value |= (long)buffer[pos++] << 8;
            value |= (long)buffer[pos++] << 16;
            value |= (long)buffer[pos++] << 24;
            value |= (long)buffer[pos++] << 32;
            value |= (long)buffer[pos++] << 40;
            value |= (long)buffer[pos++] << 48;
            value |= (long)buffer[pos++] << 56;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt32(Span<byte> buffer, ref int pos, int value)
        {
            buffer[pos++] = (byte)value;
            buffer[pos++] = (byte)(value >> 8);
            buffer[pos++] = (byte)(value >> 16);
            buffer[pos++] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32(ReadOnlySpan<byte> buffer, ref int pos)
        {
            int value = buffer[pos++];
            value |= buffer[pos++] << 8;
            value |= buffer[pos++] << 16;
            value |= buffer[pos++] << 24;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt16(Span<byte> buffer, ref int pos, short value)
        {
            buffer[pos++] = (byte)value;
            buffer[pos++] = (byte)(value >> 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadInt16(ReadOnlySpan<byte> buffer, ref int pos)
        {
            short value = buffer[pos++];
            value |= (short)(buffer[pos++] << 8);
            return value;
        }
    }
}
