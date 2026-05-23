using RuntimeLib = HandRuntime.HandCheckpointLibrary;

namespace HybridTherapist.Application.Hand;

/// <summary>
/// Application-specific checkpoint library. Delegates to <see cref="HandRuntime.HandCheckpointLibrary"/>
/// for the standard System Ping checkpoints.
///
/// Therapy-specific checkpoints use richer field structures that mirror the actual
/// clinical pipeline, so small (7B-8B) models see realistic patterns to mimic
/// rather than copying non-therapeutic placeholder values verbatim.
/// </summary>
public static class HandCheckpointLibrary
{
    /// <inheritdoc cref="HandRuntime.HandCheckpointLibrary.SystemPing"/>
    public static HandCheckpoint SystemPing => RuntimeLib.SystemPing;

    /// <inheritdoc cref="HandRuntime.HandCheckpointLibrary.MemoPing"/>
    public static HandCheckpoint MemoPing => RuntimeLib.MemoPing;

    /// <summary>
    /// L2 Analyst checkpoint — one exchange demonstrating the full M|L=2 emotional analysis
    /// wire line with realistic field names. The values are deliberately neutral so the model
    /// learns the format without mistaking the example for real clinical data.
    /// </summary>
    public static HandCheckpoint TherapyAnalystPing { get; } = new(new[]
    {
        new HandExchange("[SYSTEM_PROTOCOL_PING]",
            "M|L=2|em=neutral|sv=low|ri=none|cp=reflective|ev=\"acknowledged\""),
    });

    /// <summary>
    /// L3 Supervisor checkpoint — one exchange demonstrating the full M|L=3 clinical
    /// supervision wire line. Fields map 1:1 to the L3 dictionary (ap, tk, kq, rn).
    /// </summary>
    public static HandCheckpoint TherapySupervisorPing { get; } = new(new[]
    {
        new HandExchange("[SYSTEM_PROTOCOL_PING]",
            "M|L=3|ap=reflective_listening|tk=open_inquiry|kq=How does this feel right now?|rn=none"),
    });
}
