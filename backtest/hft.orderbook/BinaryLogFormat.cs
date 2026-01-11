using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

namespace Hft.OrderBook
{
    /// <summary>
    /// Binary log format for append-only event logging and deterministic replay.
    /// 
    /// File Format:
    /// [Header: 64 bytes]
    /// [Event 1: Variable length, 9+ bytes]
    /// [Event 2: Variable length, 9+ bytes]
    /// ...
    /// [Footer: 32 bytes]
    /// 
    /// Event Format:
    /// [Flags: 1 byte]
    /// [Timestamp: 8 bytes] (microseconds since epoch)
    /// [Payload: Variable]
    /// 
    /// Performance: Sequential writes, memory-mapped for reads.
    /// </summary>
    public sealed class BinaryLogFormat
    {
        // Magic number for file identification
        public const uint MagicNumber = 0x4F424F4B; // "OBOOK" in ASCII

        // Version for format compatibility
        public const int CurrentVersion = 2;

        // Header size
        public const int HeaderSize = 64;

        // Footer size
        public const int FooterSize = 32;

        // Maximum event size (for safety)
        public const int MaxEventSize = 256;

        // Event type flags
        private const byte EventTypeMask = 0x1F;
        private const byte CompressedFlag = 0x20;
        private const byte ReservedFlag = 0x40;
        private const byte ChecksumFlag = 0x80;

        /// <summary>
        /// Log file header structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct LogHeader
        {
            public uint Magic;           // 4 bytes: Magic number
            public ushort Version;       // 2 bytes: Format version
            public ushort Flags;         // 2 bytes: Reserved for future use
            public long InstrumentId;    // 8 bytes: Instrument ID
            public long StartTimestamp;  // 8 bytes: First event timestamp
            public long EndTimestamp;    // 8 bytes: Last event timestamp
            public long EventCount;      // 8 bytes: Total events
            public long FileSize;        // 8 bytes: Total file size
            public fixed byte Reserved[20]; // 20 bytes: Reserved
        }

        /// <summary>
        /// Log file footer structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct LogFooter
        {
            public long EventCount;      // 8 bytes: Event count (duplicate for verification)
            public long FirstSequence;   // 8 bytes: First sequence number
            public long LastSequence;    // 8 bytes: Last sequence number
            public ulong Checksum;       // 8 bytes: Adler-32 or similar
        }

        /// <summary>
        /// Event descriptor for efficient parsing.
        /// </summary>
        public readonly struct EventDescriptor
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
        }

        /// <summary>
        /// Event type constants.
        /// </summary>
        public enum LogEventType : byte
        {
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

        // ==================== Serialization ====================

        /// <summary>
        /// Serializes an audit event to binary format.
        /// Returns the number of bytes written.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SerializeEvent(Span<byte> buffer, AuditEvent evt)
        {
            if (buffer.Length < 9)
                throw new ArgumentException("Buffer too small", nameof(buffer));

            int pos = 0;

            // Write flags/event type
            buffer[pos++] = (byte)evt.EventType;

            // Write timestamp (8 bytes, little-endian)
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Timestamp);

            // Write event-specific data based on type
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

        /// <summary>
        /// Deserializes an event from binary format.
        /// Returns the number of bytes consumed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DeserializeEvent(ReadOnlySpan<byte> buffer, out AuditEvent evt)
        {
            if (buffer.Length < 9)
                throw new ArgumentException("Buffer too small", nameof(buffer));

            int pos = 0;

            // Read flags/event type
            byte flags = buffer[pos++];
            OrderEventType eventType = (OrderEventType)(flags & EventTypeMask);

            // Read timestamp
            long timestamp = BitConverterHelper.ReadInt64(buffer, ref pos);

            // Read event-specific data
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
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data1); // Price
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data2); // Quantity
            BitConverterHelper.WriteInt16(buffer, ref pos, (short)evt.Data3); // Packed attrs
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
            OrderFlags flags = (OrderFlags)((attrs >> 16) & 0xFF);

            return AuditEvent.CreateAddEvent(
                0, timestamp, orderId, 0, price, (int)qty, side, type, flags, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteCancelEvent(Span<byte> buffer, ref int pos, AuditEvent evt)
        {
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.OrderId);
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data1); // Leaves
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data2); // Original
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
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data1); // New qty
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data2); // Old qty
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data3); // New price
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data4); // Old price
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AuditEvent ReadAmendEvent(ReadOnlySpan<byte> buffer, ref int pos, long timestamp)
        {
            long orderId = BitConverterHelper.ReadInt64(buffer, ref pos);
            long newQty = BitConverterHelper.ReadInt32(buffer, ref pos);
            long oldQty = BitConverterHelper.ReadInt32(buffer, ref pos);
            long newPrice = BitConverterHelper.ReadInt64(buffer, ref pos);
            long oldPrice = BitConverterHelper.ReadInt64(buffer, ref pos);

            return AuditEvent.CreateAmendEvent(0, timestamp, orderId, 0, 
                (int)newQty, (int)oldQty, newPrice, oldPrice);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteTradeEvent(Span<byte> buffer, ref int pos, AuditEvent evt)
        {
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data1); // Price
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data2); // Qty
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data3); // Buy order
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data4); // Sell order
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
        private static AuditEvent ReadFillEvent(ReadOnlySpan<byte> buffer, ref int pos, long timestamp)
        {
            // Same format as trade
            return ReadTradeEvent(buffer, ref pos, timestamp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteBboEvent(Span<byte> buffer, ref int pos, AuditEvent evt)
        {
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data1); // Bid price
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data2); // Bid size
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data3); // Ask price
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data4); // Ask size
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
            buffer[pos++] = (byte)evt.Data1; // Reason
            BitConverterHelper.WriteInt32(buffer, ref pos, (int)evt.Data2); // Qty
            BitConverterHelper.WriteInt64(buffer, ref pos, evt.Data3); // Price
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AuditEvent ReadRejectEvent(ReadOnlySpan<byte> buffer, ref int pos, long timestamp)
        {
            long orderId = BitConverterHelper.ReadInt64(buffer, ref pos);
            long reason = buffer[pos++];
            long qty = BitConverterHelper.ReadInt32(buffer, ref pos);
            long price = BitConverterHelper.ReadInt64(buffer, ref pos);

            return AuditEvent.CreateRejectEvent(0, timestamp, orderId, 0, 
                (RejectReason)reason, (int)qty, price);
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

        // ==================== Helper Methods ====================

        /// <summary>
        /// Calculates the serialized size of an event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetEventSize(OrderEventType type)
        {
            return type switch
            {
                OrderEventType.Add => 1 + 8 + 8 + 4 + 2,  // 23 bytes
                OrderEventType.Cancel => 1 + 8 + 8 + 4 + 4, // 25 bytes
                OrderEventType.Amend => 1 + 8 + 8 + 4 + 4 + 8 + 8, // 41 bytes
                OrderEventType.Fill or OrderEventType.Trade => 1 + 8 + 8 + 4 + 8 + 8, // 37 bytes
                OrderEventType.BboChange => 1 + 8 + 8 + 4 + 8 + 4, // 33 bytes
                OrderEventType.Reject => 1 + 8 + 8 + 1 + 4 + 8, // 30 bytes
                _ => 1 + 8 + 8 + 8 // 25 bytes minimum
            };
        }

        /// <summary>
        /// Calculates Adler-32 checksum for data integrity.
        /// </summary>
        public static ulong CalculateChecksum(ReadOnlySpan<byte> data)
        {
            const uint a32 = 0xFFFF;
            const uint d32 = 0xFFFF;

            uint a = a32;
            uint d = d32;

            for (int i = 0; i < data.Length; i++)
            {
                a = (a + data[i]) % 0xFFF1;
                d = (d + a) % 0xFFF1;
            }

            return ((ulong)d << 16) | a;
        }
    }

    /// <summary>
    /// Helper for fast binary serialization.
    /// </summary>
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

