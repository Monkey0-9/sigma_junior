namespace Hft.Core
{
    /// <summary>
    /// Institutional Asset Classes for Multi-Asset Portfolio Management.
    /// Aligned with Aladdin-style risk decomposition.
    /// </summary>
    public enum AssetClass
    {
        None = 0,
        Equity = 1,
        FixedIncome = 2,
        FX = 3,
        Commodity = 4,
        Crypto = 5
    }
}
