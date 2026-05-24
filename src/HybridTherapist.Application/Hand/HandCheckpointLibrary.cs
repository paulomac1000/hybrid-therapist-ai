using RuntimeLib = HandRuntime.HandCheckpointLibrary;

namespace HybridTherapist.Application.Hand;

/// <summary>
/// Application-specific checkpoint library. Delegates to <see cref="HandRuntime.HandCheckpointLibrary"/>
/// for the standard System Ping checkpoints.
///
/// Pillar 2 — Implicit Priming (Stateless Negotiation Cache):
/// The model is NEVER told about the wire format in system prompts.
/// Instead, it sees patterns in conversation history and subconsciously continues them.
/// Each checkpoint provides 3 diverse examples so the model learns to substitute
/// real clinical data rather than copying the example verbatim.
/// </summary>
public static class HandCheckpointLibrary
{
    /// <inheritdoc cref="HandRuntime.HandCheckpointLibrary.SystemPing"/>
    public static HandCheckpoint SystemPing => RuntimeLib.SystemPing;

    /// <inheritdoc cref="HandRuntime.HandCheckpointLibrary.MemoPing"/>
    public static HandCheckpoint MemoPing => RuntimeLib.MemoPing;

    /// <summary>
    /// L2 Analyst checkpoint — three diverse exchanges demonstrating the full M|L=2
    /// wire line with varying emotional states. Values are deliberately neutral so
    /// the model learns the PATTERN (not the values) and substitutes real clinical data.
    /// </summary>
    public static HandCheckpoint TherapyAnalystPing { get; } = new(new[]
    {
        new HandExchange("[SYSTEM_PROTOCOL_PING]",
            "M|L=2|em=neutral|sv=low|ri=none|cp=reflective|ev=\"acknowledged\""),
        new HandExchange("[SYSTEM_PROTOCOL_PING]",
            "M|L=2|em=content|sv=low|ri=none|cp=grateful|ev=\"thank you\""),
        new HandExchange("[SYSTEM_PROTOCOL_PING]",
            "M|L=2|em=worried|sv=low|ri=none|cp=anticipatory|ev=\"what if\""),
    });

    /// <summary>
    /// L3 Supervisor checkpoint — three diverse exchanges demonstrating the full M|L=3
    /// wire line with different approaches and techniques. Aligned with the behavioral_activation
    /// fallback so the model is never taught a pattern that contradicts the safety net.
    /// </summary>
    public static HandCheckpoint TherapySupervisorPing { get; } = new(new[]
    {
        new HandExchange("[SYSTEM_PROTOCOL_PING]",
            "M|L=3|ap=behavioral_activation|tk=schedule_one_small_activity|kq=What one small thing could you try?|rn=none"),
        new HandExchange("[SYSTEM_PROTOCOL_PING]",
            "M|L=3|ap=sleep_hygiene|tk=no_screen_30min_before|kq=What is your bedtime routine?|rn=none"),
        new HandExchange("[SYSTEM_PROTOCOL_PING]",
            "M|L=3|ap=grounding|tk=54321_senses|kq=What do you notice right now?|rn=none"),
    });
}
