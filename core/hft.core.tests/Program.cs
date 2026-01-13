using System;
using System.Diagnostics;
using Hft.Core;

namespace Hft.Core.Tests
{
    readonly struct TestMessage
    {
        public readonly long Value;
        public TestMessage(long value) => Value = value;
    }

    class Program
    {
        static void Main()
        {
            const int messageCount = 1_000_000;
            var ring = new LockFreeRingBuffer<TestMessage>(1024 * 1024);

            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < messageCount; i++)
            {
                while (!ring.TryWrite(new TestMessage(i))) { }
            }

            for (int i = 0; i < messageCount; i++)
            {
                while (!ring.TryRead(out _)) { }
            }

            stopwatch.Stop();

            Console.WriteLine($"Processed {messageCount:N0} messages in {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Throughput: {(messageCount * 1000.0 / stopwatch.ElapsedMilliseconds):N0} msgs/sec");
        }
    }
}
