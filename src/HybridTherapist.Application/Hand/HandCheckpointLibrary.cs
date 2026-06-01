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
            "M|L=2|e7=neutral|s9=low|x4=none|y1=reflective|q3=\"acknowledged\""),
        new HandExchange("[SYSTEM_PROTOCOL_PING]",
            "M|L=2|e7=content|s9=low|x4=none|y1=grateful|q3=\"thank you\""),
        new HandExchange("[SYSTEM_PROTOCOL_PING]",
            "M|L=2|e7=worried|s9=low|x4=none|y1=anticipatory|q3=\"what if\""),
    });

    /// <summary>
    /// L3 Supervisor checkpoint — three diverse exchanges demonstrating the full M|L=3
    /// wire line with different approaches and techniques. Aligned with the behavioral_activation
    /// fallback so the model is never taught a pattern that contradicts the safety net.
    /// </summary>
    public static HandCheckpoint TherapySupervisorPing { get; } = new(new[]
    {
        new HandExchange("[SYSTEM_PROTOCOL_PING]",
            "M|L=3|p3=behavioral_activation|t5=schedule_one_small_activity|k2=What_one_small_thing_could_you_try?|r8=none"),
        new HandExchange("[SYSTEM_PROTOCOL_PING]",
            "M|L=3|p3=sleep_hygiene|t5=no_screen_30min_before|k2=What_is_your_bedtime_routine?|r8=none"),
        new HandExchange("[SYSTEM_PROTOCOL_PING]",
            "M|L=3|p3=grounding|t5=54321_senses|k2=What_do_you_notice_right_now?|r8=none"),
    });
}
