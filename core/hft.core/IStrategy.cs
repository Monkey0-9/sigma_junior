namespace Hft.Core
{
    /// <summary>
    /// Core interface for all trading strategies.
    /// Ensures a consistent callback pattern for market data updates.
    /// </summary>
    public interface IStrategy
    {
        void OnTick(ref MarketDataTick tick);
    }
}
