using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Hft.OrderBook.Tests
{
    /// <summary>
    /// Unit tests for the order book simulator.
    /// Tests deterministic matching scenarios with price-time priority.
    /// </summary>
    public class OrderBookTests
    {
        #region Basic Matching Tests

        [Fact]
        public void SimpleBuyOrder_FillsAtBestAsk()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Add a sell order at 100
            var sellOrder = OrderQueueEntry.CreateActive(
                orderId: 1, instrumentId: 1, side: OrderSide.Sell,
                price: 100, quantity: 100,
                type: OrderType.Limit, timeInForce: TimeInForce.Day,
                flags: OrderFlags.None, arrivalTimestamp: ts);
            book.ProcessOrder(sellOrder, ts + 1);

            // Act - submit a buy order at 101 (should match)
            var buyOrder = OrderQueueEntry.CreateActive(
                orderId: 2, instrumentId: 1, side: OrderSide.Buy,
                price: 101, quantity: 50,
                type: OrderType.Limit, timeInForce: TimeInForce.Day,
                flags: OrderFlags.None, arrivalTimestamp: ts + 10);

            var fills = new List<FillRecord>();
            var result = book.ProcessOrder(buyOrder, ts + 10);

            // Assert
            Assert.Single(result);
            Assert.Equal(100, result[0].Price);
            Assert.Equal(50, result[0].Quantity);
            Assert.Equal(2, result[0].AggressorOrderId);
            Assert.Equal(1, result[0].PassiveOrderId);
        }

        [Fact]
        public void SimpleSellOrder_FillsAtBestBid()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Add a buy order at 99
            var buyOrder = OrderQueueEntry.CreateActive(
                orderId: 1, instrumentId: 1, side: OrderSide.Buy,
                price: 99, quantity: 100,
                type: OrderType.Limit, timeInForce: TimeInForce.Day,
                flags: OrderFlags.None, arrivalTimestamp: ts);
            book.ProcessOrder(buyOrder, ts + 1);

            // Act - submit a sell order at 98 (should match)
            var sellOrder = OrderQueueEntry.CreateActive(
                orderId: 2, instrumentId: 1, side: OrderSide.Sell,
                price: 98, quantity: 50,
                type: OrderType.Limit, timeInForce: TimeInForce.Day,
                flags: OrderFlags.None, arrivalTimestamp: ts + 10);

            var result = book.ProcessOrder(sellOrder, ts + 10);

            // Assert
            Assert.Single(result);
            Assert.Equal(99, result[0].Price);
            Assert.Equal(50, result[0].Quantity);
        }

        [Fact]
        public void OrderDoesNotCrossSpread_GoesToBook()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Add a sell order at 100
            var sellOrder = OrderQueueEntry.CreateActive(
                orderId: 1, instrumentId: 1, side: OrderSide.Sell,
                price: 100, quantity: 100,
                type: OrderType.Limit, timeInForce: TimeInForce.Day,
                flags: OrderFlags.None, arrivalTimestamp: ts);
            book.ProcessOrder(sellOrder, ts + 1);

            // Act - submit a buy order at 99 (below best ask, should go to book)
            var buyOrder = OrderQueueEntry.CreateActive(
                orderId: 2, instrumentId: 1, side: OrderSide.Buy,
                price: 99, quantity: 50,
                type: OrderType.Limit, timeInForce: TimeInForce.Day,
                flags: OrderFlags.None, arrivalTimestamp: ts + 10);

            var result = book.ProcessOrder(buyOrder, ts + 10);

            // Assert - no fills, order added to book
            Assert.Empty(result);
            var storedOrder = book.GetOrder(2);
            Assert.NotNull(storedOrder);
            Assert.Equal(99, storedOrder.Value.Price);
            Assert.Equal(50, storedOrder.Value.LeavesQuantity);
        }

        #endregion

        #region Price-Time Priority Tests

        [Fact]
        public void MultipleOrdersAtSamePrice_FIFOOrder()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Add three sell orders at same price
            var sell1 = OrderQueueEntry.CreateActive(1, 1, OrderSide.Sell, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            var sell2 = OrderQueueEntry.CreateActive(2, 1, OrderSide.Sell, 100, 50, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 1);
            var sell3 = OrderQueueEntry.CreateActive(3, 1, OrderSide.Sell, 100, 75, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 2);

            book.ProcessOrder(sell1, ts + 1);
            book.ProcessOrder(sell2, ts + 2);
            book.ProcessOrder(sell3, ts + 3);

            // Act - buy order should match sell1 first, then sell2
            var buyOrder = OrderQueueEntry.CreateActive(10, 1, OrderSide.Buy, 101, 120, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 10);
            var result = book.ProcessOrder(buyOrder, ts + 10);

            // Assert - should have 2 fills
            Assert.Equal(2, result.Count);
            Assert.Equal(100, result[0].Price);
            Assert.Equal(100, result[0].Quantity); // Full fill of sell1
            Assert.Equal(1, result[0].PassiveOrderId);
            Assert.Equal(100, result[1].Price);
            Assert.Equal(20, result[1].Quantity); // Partial fill of sell2
            Assert.Equal(2, result[1].PassiveOrderId);
        }

        [Fact]
        public void PricePriority_BestPriceMatchesFirst()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Add sell orders at different prices
            var sell100 = OrderQueueEntry.CreateActive(1, 1, OrderSide.Sell, 100, 50, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            var sell99 = OrderQueueEntry.CreateActive(2, 1, OrderSide.Sell, 99, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 1);
            var sell101 = OrderQueueEntry.CreateActive(3, 1, OrderSide.Sell, 101, 50, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 2);

            book.ProcessOrder(sell100, ts + 1);
            book.ProcessOrder(sell99, ts + 2);
            book.ProcessOrder(sell101, ts + 3);

            // Act - buy order should match at 99 (best ask)
            var buyOrder = OrderQueueEntry.CreateActive(10, 1, OrderSide.Buy, 102, 50, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 10);
            var result = book.ProcessOrder(buyOrder, ts + 10);

            // Assert - should match at 99 (best ask)
            Assert.Single(result);
            Assert.Equal(99, result[0].Price);
            Assert.Equal(2, result[0].PassiveOrderId); // sell at 99
        }

        #endregion

        #region Partial Fill Tests

        [Fact]
        public void PartialFill_OrderRemainsInBook()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Add sell order for 100 shares
            var sellOrder = OrderQueueEntry.CreateActive(1, 1, OrderSide.Sell, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            book.ProcessOrder(sellOrder, ts + 1);

            // Act - buy order for 50 shares
            var buyOrder = OrderQueueEntry.CreateActive(2, 1, OrderSide.Buy, 101, 50, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 10);
            var result = book.ProcessOrder(buyOrder, ts + 10);

            // Assert
            Assert.Single(result);
            Assert.Equal(50, result[0].Quantity);

            // Original sell order should have 50 remaining
            var remaining = book.GetOrder(1);
            Assert.NotNull(remaining);
            Assert.Equal(50, remaining.Value.LeavesQuantity);
        }

        [Fact]
        public void MultiplePartialFills_OrderCompletelyFilled()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Add large sell order
            var sellOrder = OrderQueueEntry.CreateActive(1, 1, OrderSide.Sell, 100, 500, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            book.ProcessOrder(sellOrder, ts + 1);

            // Act - three buy orders that together fill the sell
            var buy1 = OrderQueueEntry.CreateActive(2, 1, OrderSide.Buy, 101, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 10);
            var buy2 = OrderQueueEntry.CreateActive(3, 1, OrderSide.Buy, 101, 200, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 20);
            var buy3 = OrderQueueEntry.CreateActive(4, 1, OrderSide.Buy, 101, 200, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 30);

            var result1 = book.ProcessOrder(buy1, ts + 10);
            var result2 = book.ProcessOrder(buy2, ts + 20);
            var result3 = book.ProcessOrder(buy3, ts + 30);

            // Assert
            Assert.Equal(100, result1[0].Quantity);
            Assert.Equal(200, result2[0].Quantity);
            Assert.Equal(200, result3[0].Quantity);

            // Sell order should be fully filled
            var remaining = book.GetOrder(1);
            Assert.NotNull(remaining);
            Assert.Equal(0, remaining.Value.LeavesQuantity);
            Assert.Equal(OrderStatus.Filled, remaining.Value.Status);
        }

        #endregion

        #region Cancel Tests

        [Fact]
        public void CancelOrder_RemovesFromBook()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            var order = OrderQueueEntry.CreateActive(1, 1, OrderSide.Buy, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            book.ProcessOrder(order, ts + 1);

            // Act
            var canceled = book.CancelOrder(1, ts + 10, out var canceledOrder);

            // Assert
            Assert.True(canceled);
            Assert.NotNull(canceledOrder);
            Assert.Equal(100, canceledOrder!.LeavesQuantity);

            // Order should not be in book
            Assert.Null(book.GetOrder(1));
            Assert.Equal(0, book.ActiveOrderCount);
        }

        [Fact]
        public void CancelNonExistentOrder_ReturnsFalse()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Act
            var result = book.CancelOrder(999, ts + 10, out _);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CancelOrder_UpdatesQueuePositions()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Add three buy orders
            var buy1 = OrderQueueEntry.CreateActive(1, 1, OrderSide.Buy, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            var buy2 = OrderQueueEntry.CreateActive(2, 1, OrderSide.Buy, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 1);
            var buy3 = OrderQueueEntry.CreateActive(3, 1, OrderSide.Buy, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 2);

            book.ProcessOrder(buy1, ts + 1);
            book.ProcessOrder(buy2, ts + 2);
            book.ProcessOrder(buy3, ts + 3);

            // Act - cancel middle order
            book.CancelOrder(2, ts + 10, out _);

            // Assert - remaining orders should maintain correct positions
            var remaining1 = book.GetOrder(1);
            var remaining3 = book.GetOrder(3);

            Assert.NotNull(remaining1);
            Assert.NotNull(remaining3);

            // Queue positions should be 1 and 2 (3 shifts forward)
            Assert.Equal(1, remaining1.Value.QueuePosition);
            Assert.Equal(2, remaining3.Value.QueuePosition);
        }

        #endregion

        #region Amend Tests

        [Fact]
        public void AmendOrder_UpdatesQuantity()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            var order = OrderQueueEntry.CreateActive(1, 1, OrderSide.Buy, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            book.ProcessOrder(order, ts + 1);

            // Act
            var amended = book.AmendOrder(1, 50, ts + 10, out var resultOrder);

            // Assert
            Assert.True(amended);
            Assert.NotNull(resultOrder);
            Assert.Equal(50, resultOrder!.LeavesQuantity);
        }

        [Fact]
        public void AmendOrder_ToZeroQuantity_CancelsOrder()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            var order = OrderQueueEntry.CreateActive(1, 1, OrderSide.Buy, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            book.ProcessOrder(order, ts + 1);

            // Act
            var amended = book.AmendOrder(1, 0, ts + 10, out _);

            // Assert - should fail (0 is invalid)
            Assert.False(amended);
        }

        #endregion

        #region Queue Position Tests

        [Fact]
        public void GetQueuePosition_ReturnsCorrectPosition()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            var buy1 = OrderQueueEntry.CreateActive(1, 1, OrderSide.Buy, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            var buy2 = OrderQueueEntry.CreateActive(2, 1, OrderSide.Buy, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 1);
            var buy3 = OrderQueueEntry.CreateActive(3, 1, OrderSide.Buy, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 2);

            book.ProcessOrder(buy1, ts + 1);
            book.ProcessOrder(buy2, ts + 2);
            book.ProcessOrder(buy3, ts + 3);

            // Assert
            Assert.Equal(1, book.GetQueuePosition(1));
            Assert.Equal(2, book.GetQueuePosition(2));
            Assert.Equal(3, book.GetQueuePosition(3));
        }

        [Fact]
        public void GetQuantityAhead_ReturnsCorrectQuantity()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            var buy1 = OrderQueueEntry.CreateActive(1, 1, OrderSide.Buy, 100, 50, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            var buy2 = OrderQueueEntry.CreateActive(2, 1, OrderSide.Buy, 100, 75, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 1);
            var buy3 = OrderQueueEntry.CreateActive(3, 1, OrderSide.Buy, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 2);

            book.ProcessOrder(buy1, ts + 1);
            book.ProcessOrder(buy2, ts + 2);
            book.ProcessOrder(buy3, ts + 3);

            // Assert
            Assert.Equal(0, book.GetQuantityAhead(1)); // At front
            Assert.Equal(50, book.GetQuantityAhead(2)); // 50 ahead
            Assert.Equal(125, book.GetQuantityAhead(3)); // 50+75 ahead
        }

        [Fact]
        public void GetOrdersAhead_ReturnsCorrectCount()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            var buy1 = OrderQueueEntry.CreateActive(1, 1, OrderSide.Buy, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            var buy2 = OrderQueueEntry.CreateActive(2, 1, OrderSide.Buy, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 1);
            var buy3 = OrderQueueEntry.CreateActive(3, 1, OrderSide.Buy, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 2);

            book.ProcessOrder(buy1, ts + 1);
            book.ProcessOrder(buy2, ts + 2);
            book.ProcessOrder(buy3, ts + 3);

            // Assert
            Assert.Equal(0, book.GetOrdersAhead(1));
            Assert.Equal(1, book.GetOrdersAhead(2));
            Assert.Equal(2, book.GetOrdersAhead(3));
        }

        #endregion

        #region Best Bid/Ask Tests

        [Fact]
        public void GetBestBidAsk_ReturnsCorrectBBO()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            var buy = OrderQueueEntry.CreateActive(1, 1, OrderSide.Buy, 99, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            var sell = OrderQueueEntry.CreateActive(2, 1, OrderSide.Sell, 101, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 1);

            book.ProcessOrder(buy, ts + 1);
            book.ProcessOrder(sell, ts + 2);

            // Act
            var bbo = book.GetBestBidAsk();

            // Assert
            Assert.Equal(99, bbo.BestBidPrice);
            Assert.Equal(100, bbo.BestBidSize);
            Assert.Equal(101, bbo.BestAskPrice);
            Assert.Equal(100, bbo.BestAskSize);
        }

        [Fact]
        public void BestBidAsk_UpdatesAfterFill()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Add multiple bid levels
            var buy1 = OrderQueueEntry.CreateActive(1, 1, OrderSide.Buy, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            var buy2 = OrderQueueEntry.CreateActive(2, 1, OrderSide.Buy, 99, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 1);
            var sell1 = OrderQueueEntry.CreateActive(3, 1, OrderSide.Sell, 101, 50, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 2);

            book.ProcessOrder(buy1, ts + 1);
            book.ProcessOrder(buy2, ts + 2);
            book.ProcessOrder(sell1, ts + 3);

            // Initial BBO
            var bbo1 = book.GetBestBidAsk();
            Assert.Equal(100, bbo1.BestBidPrice);
            Assert.Equal(101, bbo1.BestAskPrice);

            // Act - sell order that fully fills best bid
            var sell2 = OrderQueueEntry.CreateActive(4, 1, OrderSide.Sell, 98, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 10);
            book.ProcessOrder(sell2, ts + 10);

            // Assert - BBO should update
            var bbo2 = book.GetBestBidAsk();
            Assert.Equal(99, bbo2.BestBidPrice); // Best bid should now be at 99
            Assert.Equal(101, bbo2.BestAskPrice);
        }

        #endregion

        #region Market Order Tests

        [Fact]
        public void MarketOrder_FillsAtBestPrices()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Add multiple sell levels
            var sell1 = OrderQueueEntry.CreateActive(1, 1, OrderSide.Sell, 100, 50, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            var sell2 = OrderQueueEntry.CreateActive(2, 1, OrderSide.Sell, 101, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 1);
            var sell3 = OrderQueueEntry.CreateActive(3, 1, OrderSide.Sell, 102, 50, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 2);

            book.ProcessOrder(sell1, ts + 1);
            book.ProcessOrder(sell2, ts + 2);
            book.ProcessOrder(sell3, ts + 3);

            // Act - market buy for 150 shares
            var marketBuy = OrderQueueEntry.CreateActive(10, 1, OrderSide.Buy, 0, 150, OrderType.Market, TimeInForce.IOC, OrderFlags.None, ts + 10);
            var result = book.ProcessOrder(marketBuy, ts + 10);

            // Assert - should have 2 fills (50 at 100, 100 at 101)
            Assert.Equal(2, result.Count);
            Assert.Equal(100, result[0].Price);
            Assert.Equal(50, result[0].Quantity);
            Assert.Equal(101, result[1].Price);
            Assert.Equal(100, result[1].Quantity);
        }

        [Fact]
        public void MarketOrder_PartialFillWithIOC()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Add limited liquidity
            var sell1 = OrderQueueEntry.CreateActive(1, 1, OrderSide.Sell, 100, 50, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);

            book.ProcessOrder(sell1, ts + 1);

            // Act - IOC market buy for 100 shares (only 50 available)
            var marketBuy = OrderQueueEntry.CreateActive(10, 1, OrderSide.Buy, 0, 100, OrderType.Market, TimeInForce.IOC, OrderFlags.None, ts + 10);
            var result = book.ProcessOrder(marketBuy, ts + 10);

            // Assert - should only get 50 (partial fill expected)
            Assert.Single(result);
            Assert.Equal(50, result[0].Quantity);
        }

        #endregion

        #region Hidden Order Tests

        [Fact]
        public void HiddenOrder_NotVisibleInBook()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Add visible and hidden orders at same price
            var visible = OrderQueueEntry.CreateActive(1, 1, OrderSide.Sell, 100, 50, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            var hidden = OrderQueueEntry.CreateActive(2, 1, OrderSide.Sell, 100, 100, OrderType.Hidden, TimeInForce.Day, OrderFlags.None, ts + 1);

            book.ProcessOrder(visible, ts + 1);
            book.ProcessOrder(hidden, ts + 2);

            // Act - check BBO
            var bbo = book.GetBestBidAsk();

            // Assert - BBO should show only visible quantity
            Assert.Equal(100, bbo.BestAskPrice);
            Assert.Equal(50, bbo.BestAskSize); // Only visible quantity
        }

        [Fact]
        public void HiddenOrder_CanBeFilled()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Add only hidden order
            var hidden = OrderQueueEntry.CreateActive(1, 1, OrderSide.Sell, 100, 100, OrderType.Hidden, TimeInForce.Day, OrderFlags.None, ts);
            book.ProcessOrder(hidden, ts + 1);

            // Act - buy order should match hidden
            var buy = OrderQueueEntry.CreateActive(2, 1, OrderSide.Buy, 101, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 10);
            var result = book.ProcessOrder(buy, ts + 10);

            // Assert
            Assert.Single(result);
            Assert.Equal(100, result[0].Quantity);
            Assert.True(result[0].IsHidden);
        }

        #endregion

        #region Post-Only Tests

        [Fact]
        public void PostOnlyOrder_RejectsIfWouldTakeLiquidity()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Add resting sell order
            var sell = OrderQueueEntry.CreateActive(1, 1, OrderSide.Sell, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            book.ProcessOrder(sell, ts + 1);

            // Act - post-only buy order at price that would cross spread
            var postOnlyBuy = OrderQueueEntry.CreateActive(2, 1, OrderSide.Buy, 101, 50, OrderType.Limit, TimeInForce.Day, OrderFlags.PostOnly, ts + 10);
            
            // Should be rejected
            var result = book.ProcessOrder(postOnlyBuy, ts + 10);

            // Assert - no fill, order not in book
            Assert.Empty(result);
            Assert.Null(book.GetOrder(2));
        }

        [Fact]
        public void PostOnlyOrder_AcceptsIfWouldProvideLiquidity()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Add resting sell order at 100
            var sell = OrderQueueEntry.CreateActive(1, 1, OrderSide.Sell, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            book.ProcessOrder(sell, ts + 1);

            // Act - post-only buy order below best ask (would provide liquidity)
            var postOnlyBuy = OrderQueueEntry.CreateActive(2, 1, OrderSide.Buy, 99, 50, OrderType.Limit, TimeInForce.Day, OrderFlags.PostOnly, ts + 10);
            var result = book.ProcessOrder(postOnlyBuy, ts + 10);

            // Assert - order should be added to book
            Assert.Empty(result); // No fills (passive)
            var order = book.GetOrder(2);
            Assert.NotNull(order);
            Assert.Equal(99, order.Value.Price);
            Assert.Equal(50, order.Value.LeavesQuantity);
        }

        #endregion

        #region Determinism Tests

        [Fact]
        public void SameInput_SameOutput_Determinism()
        {
            // Arrange - two simulators with same seed
            var book1 = new OrderBook(instrumentId: 1, randomSeed: 12345);
            var book2 = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Same sequence of orders
            var orders = new[]
            {
                OrderQueueEntry.CreateActive(1, 1, OrderSide.Buy, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts),
                OrderQueueEntry.CreateActive(2, 1, OrderSide.Sell, 101, 50, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 1),
                OrderQueueEntry.CreateActive(3, 1, OrderSide.Buy, 102, 25, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 2),
            };

            // Act - process same orders
            var fills1 = new List<FillRecord>();
            var fills2 = new List<FillRecord>();

            foreach (var order in orders)
            {
                fills1.AddRange(book1.ProcessOrder(order, order.ArrivalTimestamp));
                fills2.AddRange(book2.ProcessOrder(order, order.ArrivalTimestamp));
            }

            // Assert - should have identical results
            Assert.Equal(fills1.Count, fills2.Count);
            for (int i = 0; i < fills1.Count; i++)
            {
                Assert.Equal(fills1[i].Price, fills2[i].Price);
                Assert.Equal(fills1[i].Quantity, fills2[i].Quantity);
                Assert.Equal(fills1[i].PassiveOrderId, fills2[i].PassiveOrderId);
            }
        }

        #endregion

        #region Statistics Tests

        [Fact]
        public void ActiveOrderCount_UpdatesCorrectly()
        {
            // Arrange
            var book = new OrderBook(instrumentId: 1, randomSeed: 12345);
            long ts = 1000000;

            // Assert - initial state
            Assert.Equal(0, book.ActiveOrderCount);

            // Act - add orders
            var order1 = OrderQueueEntry.CreateActive(1, 1, OrderSide.Buy, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts);
            book.ProcessOrder(order1, ts + 1);
            Assert.Equal(1, book.ActiveOrderCount);

            var order2 = OrderQueueEntry.CreateActive(2, 1, OrderSide.Buy, 100, 100, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 1);
            book.ProcessOrder(order2, ts + 2);
            Assert.Equal(2, book.ActiveOrderCount);

            // Act - add matching sell order (should trigger fills)
            var sell = OrderQueueEntry.CreateActive(3, 1, OrderSide.Sell, 100, 150, OrderType.Limit, TimeInForce.Day, OrderFlags.None, ts + 3);
            book.ProcessOrder(sell, ts + 3);

            // Assert - orders 1 and 2 partially filled, order 3 added
            Assert.Equal(2, book.ActiveOrderCount);
        }

        #endregion
    }
}

