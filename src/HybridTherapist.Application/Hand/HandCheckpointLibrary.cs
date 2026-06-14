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
    private const string ProtocolPing = "[SYSTEM_PROTOCOL_PING]";

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
        // Neutral — greeting or gratitude, no clinical data to analyse
        new HandExchange(ProtocolPing,
            "M|L=2|e7=neutral|s9=low|x4=none|y1=unspecified|q3=\"hello\""),
        // Conservative — user gave specific symptom, stay conservative on severity
        new HandExchange(ProtocolPing,
            "M|L=2|e7=fatigue|s9=low|x4=none|y1=unspecified|q3=\"can't sleep\""),
        // Detailed — rich clinical context warrants detailed analysis
        new HandExchange(ProtocolPing,
            "M|L=2|e7=anxiety|s9=high|x4=panic_fear|y1=racing_thoughts|q3=\"constantly worried about everything\""),
    });

    /// <summary>
    /// L3 Supervisor checkpoint — three diverse exchanges demonstrating the full M|L=3
    /// wire line with different approaches and techniques. Aligned with the behavioral_activation
    /// fallback so the model is never taught a pattern that contradicts the safety net.
    /// </summary>
    public static HandCheckpoint TherapySupervisorPing { get; } = new(new[]
    {
        new HandExchange(ProtocolPing,
            "M|L=3|p3=behavioral_activation|t5=schedule_one_small_activity|k2=What_one_small_thing_could_you_try?|r8=none"),
        new HandExchange(ProtocolPing,
            "M|L=3|p3=sleep_hygiene|t5=no_screen_30min_before|k2=What_is_your_bedtime_routine?|r8=none"),
        new HandExchange(ProtocolPing,
            "M|L=3|p3=grounding|t5=54321_senses|k2=What_do_you_notice_right_now?|r8=none"),
    });

    /// <summary>
    /// L2 Analyst checkpoint for H.A.N.D. Semantic variant.
    /// Uses human-readable semantic keys instead of H.A.N.D. Compact two-character keys.
    /// </summary>
    public static HandCheckpoint TherapyAnalystSemanticPing { get; } = new(new[]
    {
        new HandExchange(ProtocolPing,
            "M|L=2|em=neutral|sv=low|ri=none|cp=reflective|ev=\"acknowledged\""),
        new HandExchange(ProtocolPing,
            "M|L=2|em=content|sv=low|ri=none|cp=grateful|ev=\"thank you\""),
        new HandExchange(ProtocolPing,
            "M|L=2|em=worried|sv=low|ri=none|cp=anticipatory|ev=\"what if\""),
    });

    /// <summary>
    /// L3 Supervisor checkpoint for H.A.N.D. Semantic variant.
    /// Uses human-readable semantic keys instead of H.A.N.D. Compact two-character keys.
    /// </summary>
    public static HandCheckpoint TherapySupervisorSemanticPing { get; } = new(new[]
    {
        new HandExchange(ProtocolPing,
            "M|L=3|ap=behavioral_activation|tk=schedule_one_small_activity|kq=What_one_small_thing_could_you_try?|rn=none"),
        new HandExchange(ProtocolPing,
            "M|L=3|ap=sleep_hygiene|tk=no_screen_30min_before|kq=What_is_your_bedtime_routine?|rn=none"),
        new HandExchange(ProtocolPing,
            "M|L=3|ap=grounding|tk=54321_senses|kq=What_do_you_notice_right_now?|rn=none"),
    });

    /// <summary>
    /// L2 Analyst checkpoint for Plaintext variant.
    /// No wire format; emits structured prose paragraph.
    /// </summary>
    public static HandCheckpoint TherapyAnalystPlaintextPing { get; } = new(new[]
    {
        new HandExchange(ProtocolPing,
            "Emotional state: neutral. Severity: low. Risk: none. Patterns: reflective. Evidence: 'acknowledged'."),
        new HandExchange(ProtocolPing,
            "Emotional state: content. Severity: low. Risk: none. Patterns: grateful. Evidence: 'thank you'."),
        new HandExchange(ProtocolPing,
            "Emotional state: worried. Severity: low. Risk: none. Patterns: anticipatory. Evidence: 'what if'."),
    });

    /// <summary>
    /// L3 Supervisor checkpoint for Plaintext variant.
    /// No wire format; emits structured prose paragraph.
    /// </summary>
    public static HandCheckpoint TherapySupervisorPlaintextPing { get; } = new(new[]
    {
        new HandExchange(ProtocolPing,
            "Approach: behavioral_activation. Technique: schedule_one_small_activity. Key question: What one small thing could you try? Risk note: none."),
        new HandExchange(ProtocolPing,
            "Approach: sleep_hygiene. Technique: no_screen_30min_before. Key question: What is your bedtime routine? Risk note: none."),
        new HandExchange(ProtocolPing,
            "Approach: grounding. Technique: 54321_senses. Key question: What do you notice right now? Risk note: none."),
    });

    /// <summary>
    /// L2 Analyst checkpoint for JSON variant.
    /// Emits valid JSON strings.
    /// </summary>
    public static HandCheckpoint TherapyAnalystJsonPing { get; } = new(new[]
    {
        new HandExchange(ProtocolPing,
            "{\"layer\":2,\"emotional_state\":\"neutral\",\"severity\":\"low\",\"risk\":\"none\",\"patterns\":\"reflective\",\"evidence\":\"acknowledged\"}"),
        new HandExchange(ProtocolPing,
            "{\"layer\":2,\"emotional_state\":\"content\",\"severity\":\"low\",\"risk\":\"none\",\"patterns\":\"grateful\",\"evidence\":\"thank you\"}"),
        new HandExchange(ProtocolPing,
            "{\"layer\":2,\"emotional_state\":\"worried\",\"severity\":\"low\",\"risk\":\"none\",\"patterns\":\"anticipatory\",\"evidence\":\"what if\"}"),
    });

    /// <summary>
    /// L3 Supervisor checkpoint for JSON variant.
    /// Emits valid JSON strings.
    /// </summary>
    public static HandCheckpoint TherapySupervisorJsonPing { get; } = new(new[]
    {
        new HandExchange(ProtocolPing,
            "{\"layer\":3,\"approach\":\"behavioral_activation\",\"technique\":\"schedule_one_small_activity\",\"key_question\":\"What one small thing could you try?\",\"risk_note\":\"none\"}"),
        new HandExchange(ProtocolPing,
            "{\"layer\":3,\"approach\":\"sleep_hygiene\",\"technique\":\"no_screen_30min_before\",\"key_question\":\"What is your bedtime routine?\",\"risk_note\":\"none\"}"),
        new HandExchange(ProtocolPing,
            "{\"layer\":3,\"approach\":\"grounding\",\"technique\":\"54321_senses\",\"key_question\":\"What do you notice right now?\",\"risk_note\":\"none\"}"),
    });
}
