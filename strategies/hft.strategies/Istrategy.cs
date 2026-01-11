using Hft.Core;

namespace Hft.Strategies
{
    public interface IStrategy
    {
        void OnTick(ref MarketDataTick tick);
    }
}
