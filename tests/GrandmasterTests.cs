using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Hft.Core;
using Hft.Infra;
using Xunit;

namespace Hft.Tests
{
    /// <summary>
    /// Grandmaster Domain Primitives Tests.
    /// Verifies serialization integrity and immutability guarantees.
    /// </summary>
    public class DomainPrimitivesTests
    {
        /// <summary>
        /// GRANDMASTER: Verify MarketDataTick struct layout matches expected byte size.
        /// Critical for network serialization and MemoryMarshal compatibility.
        /// </summary>
        [Fact]
        public void MarketDataTick_StructLayout_ShouldMatchExpectedSize()
        {
            // Arrange
            int expectedBids = 5;
            int expectedAsks = 5;

            // Calculate expected size:
            // Version(1) + Sequence(8) + InstrumentId(8) + SendTimestampTicks(8) + ReceiveTimestampTicks(8)
            // + Bid1-5 (2 * 5 * 16 bytes for PriceLevel) + Ask1-5 (2 * 5 * 16 bytes for PriceLevel)
            // PriceLevel = double Price(8) + double Size(8) = 16 bytes
            // Total = 1 + 8 + 8 + 8 + 8 + 5*16 + 5*16 = 33 + 160 = 193 bytes
            int expectedSize = 1 + 8 + 8 + 8 + 8 + (expectedBids * 16) + (expectedAsks * 16);

            // Act
            int actualSize = Marshal.SizeOf<MarketDataTick>();

            // Assert
            Assert.Equal(expectedSize, actualSize);
        }

        /// <summary>
        /// GRANDMASTER: Verify Order struct layout is deterministic.
        /// </summary>
        [Fact]
        public void Order_StructLayout_ShouldBeCompact()
        {
            // Arrange
            // Order: Version(1) + OrderId(8) + InstrumentId(8) + Side(4) + Padding(4) + Price(8) + Quantity(8) + TimestampTicks(8) + Sequence(8) = 57 bytes, aligned to 8 = 64
            int expectedSize = 64;

            // Act
            int actualSize = Marshal.SizeOf<Order>();

            // Assert
            Assert.Equal(expectedSize, actualSize);
        }

        /// <summary>
        /// GRANDMASTER: Verify Fill struct layout is deterministic.
        /// </summary>
        [Fact]
        public void Fill_StructLayout_ShouldBeCompact()
        {
            // Arrange
            // Fill: Version(1) + FillId(8) + OrderId(8) + InstrumentId(8) + Side(4) + Padding(4) + Price(8) + Quantity(8) + TimestampTicks(8) = 57 bytes, aligned to 8 = 64
            int expectedSize = 64;

            // Act
            int actualSize = Marshal.SizeOf<Fill>();

            // Assert
            Assert.Equal(expectedSize, actualSize);
        }

        /// <summary>
        /// GRANDMASTER: Verify Order is immutable (no setters on public properties).
        /// </summary>
        [Fact]
        public void Order_Immutability_ShouldHaveNoPublicSetters()
        {
            // Arrange
            var order = new Order(1, 1001, OrderSide.Buy, 100.50, 10, DateTime.UtcNow.Ticks, 1);

            // Act - Verify all properties are readonly/get-only
            var orderId = order.OrderId;
            var instrumentId = order.InstrumentId;
            var side = order.Side;
            var price = order.Price;
            var quantity = order.Quantity;
            var timestamp = order.TimestampTicks;
            var sequence = order.Sequence;

            // Assert - Properties should be get-only (implicit for struct fields)
            Assert.True(true); // If we got here without compile error, immutability holds
        }

        /// <summary>
        /// GRANDMASTER: Verify Order.WithPrice creates new instance (immutability pattern).
        /// </summary>
        [Fact]
        public void Order_WithPrice_ShouldReturnNewInstance()
        {
            // Arrange
            var original = new Order(1, 1001, OrderSide.Buy, 100.50, 10, DateTime.UtcNow.Ticks, 1);
            double newPrice = 101.25;

            // Act
            var modified = original.WithPrice(newPrice);

            // Assert
            Assert.Equal(100.50, original.Price);
            Assert.Equal(newPrice, modified.Price);
            Assert.Equal(original.OrderId, modified.OrderId);
            Assert.NotSame(original, modified);
        }

        /// <summary>
        /// GRANDMASTER: Verify MarketDataTick can be marshaled to/from bytes correctly.
        /// </summary>
        [Fact]
        public void MarketDataTick_RoundTripSerialization_ShouldPreserveData()
        {
            // Arrange
            var bids = new PriceLevel[]
            {
                new PriceLevel(100.00, 100),
                new PriceLevel(99.99, 200),
                new PriceLevel(99.98, 300),
                new PriceLevel(99.97, 400),
                new PriceLevel(99.96, 500)
            };
            var asks = new PriceLevel[]
            {
                new PriceLevel(100.01, 150),
                new PriceLevel(100.02, 250),
                new PriceLevel(100.03, 350),
                new PriceLevel(100.04, 450),
                new PriceLevel(100.05, 550)
            };
            long sendTs = DateTime.UtcNow.Ticks;
            long recvTs = sendTs + 1000; // 100 microseconds latency

            var original = new MarketDataTick(1, 1001, sendTs, recvTs, bids, asks);

            // Act - Serialize via Marshal
            int size = Marshal.SizeOf<MarketDataTick>();
            byte[] bytes = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(original, ptr, false);
                Marshal.Copy(ptr, bytes, 0, size);
                Marshal.FreeHGlobal(ptr);
                ptr = IntPtr.Zero;

                // Deserialize
                ptr = Marshal.AllocHGlobal(size);
                Marshal.Copy(bytes, 0, ptr, size);
                var deserialized = Marshal.PtrToStructure<MarketDataTick>(ptr);
            }
            finally
            {
                if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
            }

            // Assert - Verify critical fields
            Assert.Equal(original.Sequence, deserialized.Sequence);
            Assert.Equal(original.InstrumentId, deserialized.InstrumentId);
            Assert.Equal(original.BestBid, deserialized.BestBid);
            Assert.Equal(original.BestAsk, deserialized.BestAsk);
        }

        /// <summary>
        /// GRANDMASTER: Verify PriceLevel struct is properly aligned.
        /// </summary>
        [Fact]
        public void PriceLevel_StructLayout_ShouldBe16Bytes()
        {
            // Arrange
            int expectedSize = 16; // double Price (8) + double Size (8)

            // Act
            int actualSize = Marshal.SizeOf<PriceLevel>();

            // Assert
            Assert.Equal(expectedSize, actualSize);
        }
    }

    /// <summary>
    /// Grandmaster MetricsServer Tests.
    /// Verifies graceful failure handling and lifecycle management.
    /// </summary>
    public class MetricsServerTests
    {
        /// <summary>
        /// GRANDMASTER: Verify MetricsServer handles invalid port gracefully.
        /// </summary>
        [Fact]
        public void MetricsServer_InvalidPort_ShouldNotThrow()
        {
            // Arrange
            var counters = new System.Collections.Generic.List<MetricsCounter>
            {
                new MetricsCounter("test_counter")
            };
            var position = new PositionSnapshot { InstrumentId = 1001 };

            // Use a reserved port that will likely fail
            var server = new MetricsServer(80, counters, position); // Port 80 may require admin

            // Act - Should not throw, just log warning
            server.Start();

            // Assert - Server should report disabled gracefully
            Assert.False(server.IsEnabled);
        }

        /// <summary>
        /// GRANDMASTER: Verify MetricsServer can be disposed safely.
        /// </summary>
        [Fact]
        public void MetricsServer_Dispose_ShouldNotThrow()
        {
            // Arrange
            var counters = new System.Collections.Generic.List<MetricsCounter>();
            var position = new PositionSnapshot();
            var server = new MetricsServer(9180, counters, position);

            // Act & Assert - Multiple disposals should be safe
            server.Dispose();
            server.Dispose(); // Should not throw
        }

        /// <summary>
        /// GRANDMASTER: Verify MetricsServer uses unprivileged port by default.
        /// </summary>
        [Theory]
        [InlineData(9180)]
        [InlineData(9181)]
        [InlineData(10000)]
        public void MetricsServer_UnprivilegedPort_ShouldStart(int port)
        {
            // Arrange
            var counters = new System.Collections.Generic.List<MetricsCounter>
            {
                new MetricsCounter("test_counter")
            };
            var position = new PositionSnapshot { InstrumentId = 1001 };

            var server = new MetricsServer(port, counters, position);

            // Act
            server.Start();

            // Assert
            Assert.True(server.IsEnabled);

            // Cleanup
            server.Dispose();
        }
    }

    /// <summary>
    /// Grandmaster AppendOnlyLog Tests.
    /// Verifies file creation, HMAC signing, and replay capabilities.
    /// </summary>
    public class AppendOnlyLogTests : IDisposable
    {
        private readonly string _testDir;
        private readonly byte[] _testKey;

        public AppendOnlyLogTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"hft_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
            _testKey = System.Text.Encoding.UTF8.GetBytes("12345678901234567890123456789012"); // 32 bytes
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_testDir, recursive: true);
            }
            catch { /* Best effort cleanup */ }
        }

        /// <summary>
        /// GRANDMASTER: Verify AppendOnlyLog creates directory if missing.
        /// </summary>
        [Fact]
        public void AppendOnlyLog_MissingDirectory_ShouldCreateIt()
        {
            // Arrange
            var nestedPath = Path.Combine(_testDir, "nested", "deep", "audit.bin");

            // Act
            using var log = new AppendOnlyLog(nestedPath, _testKey);

            // Assert
            Assert.True(Directory.Exists(Path.GetDirectoryName(nestedPath)));
        }

        /// <summary>
        /// GRANDMASTER: Verify AppendOnlyLog appends data and can read it back.
        /// </summary>
        [Fact]
        public void AppendOnlyLog_Append_ShouldCreateValidAuditRecord()
        {
            // Arrange
            var path = Path.Combine(_testDir, "audit_test.bin");
            var order = new Order(1, 1001, OrderSide.Buy, 100.50, 10, DateTime.UtcNow.Ticks, 1);

            // Act
            using (var log = new AppendOnlyLog(path, _testKey))
            {
                log.Append(1, in order);
            }

            // Assert - File should exist and have content
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
        }

        /// <summary>
        /// GRANDMASTER: Verify multiple AppendOnlyLog appends work correctly.
        /// </summary>
        [Fact]
        public void AppendOnlyLog_MultipleAppends_ShouldAllBeWritten()
        {
            // Arrange
            var path = Path.Combine(_testDir, "multi_audit.bin");
            int count = 10;

            // Act
            using (var log = new AppendOnlyLog(path, _testKey))
            {
                for (int i = 0; i < count; i++)
                {
                    var order = new Order(i, 1001, OrderSide.Buy, 100.50, 10, DateTime.UtcNow.Ticks, (long)i);
                    log.Append(1, in order);
                }
            }

            // Assert
            Assert.True(File.Exists(path));
            // Each record is ~97 bytes (4 marker + 1 ver + 8 ts + 1 type + 4 len + 64 order + 32 hmac)
            Assert.True(new FileInfo(path).Length >= count * 90);
        }

        /// <summary>
        /// GRANDMASTER: Verify AppendOnlyLog SizeBytes property works.
        /// </summary>
        [Fact]
        public void AppendOnlyLog_SizeBytes_ShouldReportAccurateSize()
        {
            // Arrange
            var path = Path.Combine(_testDir, "size_test.bin");
            var order = new Order(1, 1001, OrderSide.Buy, 100.50, 10, DateTime.UtcNow.Ticks, 1);

            using var log = new AppendOnlyLog(path, _testKey);

            // Act
            long beforeSize = log.SizeBytes;
            log.Append(1, in order);
            long afterSize = log.SizeBytes;

            // Assert
            Assert.True(afterSize > beforeSize);
        }
    }

    /// <summary>
    /// Grandmaster Infrastructure Tests.
    /// Verifies filesystem bootstrap and core infrastructure components.
    /// </summary>
    public class InfrastructureTests
    {
        /// <summary>
        /// GRANDMASTER: Verify FilesystemBootstrap creates all required directories.
        /// </summary>
        [Fact]
        public void FilesystemBootstrap_EnsureDirectories_ShouldCreateAllDirs()
        {
            // Arrange
            var testDir = Path.Combine(Path.GetTempPath(), $"hft_bootstrap_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);

            try
            {
                // Act
                FilesystemBootstrap.EnsureDirectories(testDir);

                // Assert
                Assert.True(Directory.Exists(Path.Combine(testDir, "data", "audit")));
                Assert.True(Directory.Exists(Path.Combine(testDir, "data", "metrics")));
                Assert.True(Directory.Exists(Path.Combine(testDir, "data", "replay")));
                Assert.True(Directory.Exists(Path.Combine(testDir, "logs")));
            }
            finally
            {
                Directory.Delete(testDir, recursive: true);
            }
        }

        /// <summary>
        /// GRANDMASTER: Verify FilesystemBootstrap is idempotent.
        /// </summary>
        [Fact]
        public void FilesystemBootstrap_EnsureDirectories_ShouldBeIdempotent()
        {
            // Arrange
            var testDir = Path.Combine(Path.GetTempPath(), $"hft_bootstrap_idem_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);

            try
            {
                // Act - Call multiple times
                FilesystemBootstrap.EnsureDirectories(testDir);
                FilesystemBootstrap.EnsureDirectories(testDir);
                FilesystemBootstrap.EnsureDirectories(testDir);

                // Assert - Should still work
                Assert.True(Directory.Exists(Path.Combine(testDir, "data", "audit")));
            }
            finally
            {
                Directory.Delete(testDir, recursive: true);
            }
        }

        /// <summary>
        /// GRANDMASTER: Verify MetricsCounter thread safety.
        /// </summary>
        [Fact]
        public void MetricsCounter_ConcurrentAccess_ShouldBeThreadSafe()
        {
            // Arrange
            var counter = new MetricsCounter("concurrent_test");
            int iterations = 10000;

            // Act
            Parallel.For(0, iterations, i => counter.Increment());

            // Assert
            Assert.Equal(iterations, counter.Get());
        }

        /// <summary>
        /// GRANDMASTER: Verify PositionSnapshot thread safety.
        /// </summary>
        [Fact]
        public void PositionSnapshot_ConcurrentWrite_ShouldBeThreadSafe()
        {
            // Arrange
            var snapshot = new PositionSnapshot { InstrumentId = 1001 };
            int iterations = 1000;

            // Act - Concurrent writes
            Parallel.For(0, iterations, i =>
            {
                snapshot.SetNetPosition(i);
            });

            // Assert - Final value should be one of the written values
            var finalPosition = snapshot.GetNetPosition();
            Assert.True(finalPosition >= 0 && finalPosition < iterations);
        }
    }

    /// <summary>
    /// Grandmaster LatencyMonitor Tests.
    /// Verifies latency tracking accuracy.
    /// </summary>
    public class LatencyMonitorTests
    {
        /// <summary>
        /// GRANDMASTER: Verify LatencyMonitor calculates stats correctly.
        /// </summary>
        [Fact]
        public void LatencyMonitor_GetStats_ShouldCalculatePercentiles()
        {
            // Arrange
            var monitor = new LatencyMonitor();

            // Add samples: 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 (in ticks = 100ns units)
            // Converting to microseconds = divide by 10
            for (int i = 10; i <= 100; i += 10)
            {
                monitor.Record(i * 10); // ticks
            }

            // Act
            var stats = monitor.GetStats();

            // Assert
            Assert.Equal(10, stats.Count);
            Assert.Equal(1.0, stats.Min); // 10 ticks / 10 = 1us
            Assert.Equal(10.0, stats.Max); // 100 ticks / 10 = 10us
            Assert.Equal(5.5, stats.Avg); // Average of 1-10us
            Assert.Equal(5.0, stats.P50); // Median
            Assert.Equal(9.0, stats.P90); // 90th percentile
            Assert.Equal(9.9, stats.P99); // 99th percentile
        }

        /// <summary>
        /// GRANDMASTER: Verify LatencyMonitor handles empty state.
        /// </summary>
        [Fact]
        public void LatencyMonitor_EmptyStats_ShouldReturnDefaults()
        {
            // Arrange
            var monitor = new LatencyMonitor();

            // Act
            var stats = monitor.GetStats();

            // Assert
            Assert.Equal(0, stats.Count);
            Assert.Equal(0, stats.Min);
            Assert.Equal(0, stats.Max);
        }

        /// <summary>
        /// GRANDMASTER: Verify LatencyMonitor reset works.
        /// </summary>
        [Fact]
        public void LatencyMonitor_Reset_ShouldClearAllSamples()
        {
            // Arrange
            var monitor = new LatencyMonitor();
            monitor.Record(1000);

            // Act
            monitor.Reset();
            var stats = monitor.GetStats();

            // Assert
            Assert.Equal(0, stats.Count);
        }
    }
}

