using HandCodec.Parser;

namespace HybridTherapist.Application.Hand;

/// <summary>
/// Domain-specific extension methods for the H.A.N.D. <see cref="MemoBuilder"/>,
/// keeping the core codec 100% domain-agnostic.
/// </summary>
public static class TherapistMemoBuilderExtensions
{
    public static MemoBuilder EmotionalState(this MemoBuilder builder, string value) =>
        builder.Field("e7", "e7", "emotional_state", value);

    public static MemoBuilder Severity(this MemoBuilder builder, string value) =>
        builder.Field("s9", "s9", "severity", value);

    public static MemoBuilder Approach(this MemoBuilder builder, string value) =>
        builder.Field("p3", "p3", "approach", value);

    public static MemoBuilder KeyQuestion(this MemoBuilder builder, string value) =>
        builder.Field("k2", "k2", "key_question", value);

    public static MemoBuilder RiskIndicators(this MemoBuilder builder, string value) =>
        builder.Field("x4", "x4", "risk_indicators", value);

    public static MemoBuilder CognitivePatterns(this MemoBuilder builder, string value) =>
        builder.Field("y1", "y1", "cognitive_patterns", value);

    public static MemoBuilder EvidenceQuotes(this MemoBuilder builder, string value) =>
        builder.Field("q3", "q3", "evidence_quotes", value);

    public static MemoBuilder Technique(this MemoBuilder builder, string value) =>
        builder.Field("t5", "t5", "technique", value);

    public static MemoBuilder SessionGoal(this MemoBuilder builder, string value) =>
        builder.Field("g6", "g6", "session_goal", value);

    public static MemoBuilder RiskNote(this MemoBuilder builder, string value) =>
        builder.Field("r8", "r8", "risk_note", value);

    public static MemoBuilder CrisisFlag(this MemoBuilder builder, string value) =>
        builder.Field("f0", "f0", "crisis_flag", value);
}
