using Hft.Core.Audit;

namespace Hft.Core
{
    public enum EventSeverity
    {
        None = 0,
        Info,
        Warning,
        Error,
        Critical
    }

    public interface IEventLogger
    {
        void LogInfo(string component, string message);
        void LogWarning(string component, string message);
        void LogError(string component, string message);

        // Structured events
        void LogOrder(AuditRecordType type, in Order order);
        void LogFill(in Fill fill);
        void LogRiskEvent(string rule, string action, string message);
        void LogPnlUpdate(long instrumentId, double netPos, double realizedPnl, double unrealizedPnl);
        void LogTick(in MarketDataTick tick);
    }
}

