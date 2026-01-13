using System;
using System.Collections.Generic;

namespace Hft.Core;

/// <summary>
/// Represents the current state of a portfolio for risk checks.
/// </summary>
public record PortfolioState(
    long PortfolioId,
    double CashBalance,
    double TotalExposure,
    IReadOnlyDictionary<long, double> Positions,
    IReadOnlyDictionary<long, double> OpenOrdersExposure
);
