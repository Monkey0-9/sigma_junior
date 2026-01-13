using System;
using System.Collections.Generic;
using System.Linq;
using Hft.Core;

namespace Hft.Execution
{
    /// <summary>
    /// Advanced Execution Simulator for testing optimal execution strategies.
    /// Simulates parent order execution against a realistic market impact model.
    /// </summary>
    public sealed class ExecutionSimulator
    {
        public record SimulationResult(
            double TotalFilled,
            double AveragePrice,
            double ImplementationShortfallBps,
            List<FillRecord> Fills
        );

        public record MarketState(
            double MidPrice,
            double SpreadBps,
            double VolatilityMinutes,
            double TempImpactCoeff,
            double PermImpactCoeff
        );

        private readonly Random _random = new(42); // Deterministic for simulation

        /// <summary>
        /// Simulates execution of a parent order using a provided schedule.
        /// </summary>
        public SimulationResult SimulateExecution(
            OrderSide side,
            double totalQuantity,
            double[] schedule,
            MarketState market)
        {
            double currentMidPrice = market.MidPrice;
            double arrivalPrice = market.MidPrice;
            double totalFilled = 0;
            double totalCost = 0;
            var fills = new List<FillRecord>();

            for (int i = 0; i < schedule.Length; i++)
            {
                double nk = schedule[i];
                if (nk <= 0) continue;

                // 1. Calculate Market Impact
                // Permanent impact: price moves by gamma * nk
                double permImpact = market.PermImpactCoeff * nk;
                currentMidPrice += (side == OrderSide.Buy ? 1 : -1) * permImpact;

                // Temporary impact: price observed for this interval is shifted by eta * (nk/tau)
                // In our schedule, nk is the quantity for the interval.
                double tempImpact = market.TempImpactCoeff * nk;
                
                double executionPrice;
                if (side == OrderSide.Buy)
                {
                    executionPrice = currentMidPrice + (market.MidPrice * (market.SpreadBps / 10000.0) / 2.0) + tempImpact;
                }
                else
                {
                    executionPrice = currentMidPrice - (market.MidPrice * (market.SpreadBps / 10000.0) / 2.0) - tempImpact;
                }

                // Add some noise based on volatility
                double noise = market.VolatilityMinutes * currentMidPrice * (Math.Sqrt(1.0) * (_random.NextDouble() * 2 - 1));
                executionPrice += noise;

                totalFilled += nk;
                totalCost += nk * executionPrice;

                fills.Add(new FillRecord(
                    i, i, DateTime.UtcNow.Ticks, 0, 0, 0, (long)executionPrice, (int)nk, side, false, false, LiquidityType.Taker
                ));
            }

            double averagePrice = totalCost / totalFilled;
            double IS;
            if (side == OrderSide.Buy)
            {
                IS = (averagePrice - arrivalPrice) / arrivalPrice;
            }
            else
            {
                IS = (arrivalPrice - averagePrice) / arrivalPrice;
            }

            return new SimulationResult(
                totalFilled,
                averagePrice,
                IS * 10000.0,
                fills
            );
        }
    }

    /// <summary>
    /// Minimal FillRecord for simulation if not using the one from Hft.OrderBook
    /// Note: I'll use the one from Hft.OrderBook if possible, but here for reference/independence.
    /// In this project, I might need to reference Hft.OrderBook.
    /// </summary>
    public enum LiquidityType { Maker, Taker }
    public record FillRecord(
        long FillId,
        long SequenceNumber,
        long Timestamp,
        long AggressorOrderId,
        long PassiveOrderId,
        long InstrumentId,
        long Price,
        int Quantity,
        OrderSide Side,
        bool IsHidden,
        bool IsMidPoint,
        LiquidityType Liquidity
    );
}
