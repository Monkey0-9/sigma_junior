using System.Diagnostics;

namespace Hft.Core
{
    /// <summary>
    /// Centralized ActivitySource for OpenTelemetry tracing.
    /// Following semantic conventions for HFT operations.
    /// </summary>
    public static class HftTracing
    {
        private const string SourceName = "Hft.Platform";
        public static readonly ActivitySource Source = new(SourceName);

        // Common Span Names
        public const string SignalGeneration = "SignalGeneration";
        public const string OrderExecution = "OrderExecution";
        public const string RiskCheck = "RiskCheck";
        public const string MarketDataProcess = "MarketDataProcess";
    }
}
