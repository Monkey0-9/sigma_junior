using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Hft.Core;
using Hft.Risk;

namespace Hft.Benchmarks
{
    [MemoryDiagnoser]
    public class RingBufferBenchmark
    {
        private LockFreeRingBuffer<int>? _ring;
        private int _data = 42;

        [GlobalSetup]
        public void Setup()
        {
            _ring = new LockFreeRingBuffer<int>(4096);
        }

        [Benchmark]
        public void TryWriteTryRead()
        {
            _ring!.TryWrite(_data);
            _ring!.TryRead(out _);
        }
    }

    internal static class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<RingBufferBenchmark>();
        }
    }
}

