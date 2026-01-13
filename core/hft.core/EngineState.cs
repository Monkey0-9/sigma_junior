namespace Hft.Core
{
    /// <summary>
    /// Institutional state machine for an HFT Trading Node.
    /// Every state transition must be logged for audit.
    /// </summary>
    public enum EngineState
    {
        None = 0,
        Init = 1,
        Standby = 2,
        Trading = 3,
        Degraded = 4,
        Stopping = 5,
        Stopped = 6
    }
}
