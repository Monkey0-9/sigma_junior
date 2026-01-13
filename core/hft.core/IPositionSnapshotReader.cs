using System.Runtime.CompilerServices;

namespace Hft.Core
{
    /// <summary>
    /// Read-only interface for Position state.
    /// Ensures Strategy and Execution cannot mutate position state.
    /// Aligned with Aladdin's strict state ownership.
    /// </summary>
    public interface IPositionSnapshotReader
    {
        long InstrumentId { get; }
        double NetPosition { get; }
        double AvgEntryPrice { get; }
        double RealizedPnL { get; }
        double UnrealizedPnL { get; }
        double TotalPnL { get; }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        PositionSnapshot Copy();
    }
}
