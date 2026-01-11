# HFT Platform Fixes - TODO List

## Step 1 - Delete Duplicate Files
- [x] Delete `core/hft.core/LockFreeRingBuffer.cs`
- [x] Delete `feeds/hft.feeds/MarketDataTick.cs`
- [x] Delete `strategies/hft.strategies/Order.cs`

## Step 2 - Update Hft.Core.MarketDataTick
- [x] Update to match feeds version with Price/Size fields

## Step 3 - Fix Using Statements
- [x] Update `MarketMakerStrategy.cs` - remove `using Hft.Feeds`
- [x] Update `Echostrategy.cs` - remove `using Hft.Feeds`
- [x] Update `UdpMarketDataListener.cs` to use `Hft.Core`
- [x] Update `feeds/hft.feeds/Udp/UdpMarketDataListener.cs` to use `Hft.Core`

## Step 4 - Fix CS8618 Warnings
- [x] `UdpMarketDataListener.cs` already has Task initialization
- [x] `MetricsServer.cs` - added Task.CompletedTask initialization

## Step 5 - Fix PnlEngine
- [x] Update `Hft.Runner/Program.cs` to use single Price parameter

## Step 6 - Build & Test
- [ ] Run `dotnet clean`
- [ ] Run `dotnet build`
- [ ] Run `dotnet run -c Release`

