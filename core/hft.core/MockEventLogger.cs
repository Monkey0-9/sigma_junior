using System;

using Hft.Core.Audit;

namespace Hft.Core
{
    public class MockEventLogger : IEventLogger
    {
        public void LogInfo(string component, string message) { }
        public void LogWarning(string component, string message) { }
        public void LogError(string component, string message) { }
        public void LogOrder(AuditRecordType type, in Order order) { }
        public void LogFill(in Fill fill) { }
        public void LogRiskEvent(string rule, string action, string message) { }
        public void LogPnlUpdate(long instrumentId, double netPos, double realizedPnl, double unrealizedPnl) { }
        public void LogTick(in MarketDataTick tick) { }
    }
}
