using HandCodec.Parser;

namespace HybridTherapist.Application.Hand;

/// <summary>
/// Domain-specific extension methods for the H.A.N.D. <see cref="MemoBuilder"/>,
/// keeping the core codec 100% domain-agnostic.
/// </summary>
public static class TherapistMemoBuilderExtensions
{
    public static MemoBuilder EmotionalState(this MemoBuilder builder, string value) =>
        builder.Field("e", "em", "emotional_state", value);

    public static MemoBuilder Severity(this MemoBuilder builder, string value) =>
        builder.Field("s", "sv", "severity", value);

    public static MemoBuilder Approach(this MemoBuilder builder, string value) =>
        builder.Field("a", "ap", "approach", value);

    public static MemoBuilder KeyQuestion(this MemoBuilder builder, string value) =>
        builder.Field("k", "kq", "key_question", value);

    public static MemoBuilder RiskIndicators(this MemoBuilder builder, string value) =>
        builder.Field("r", "ri", "risk_indicators", value);

    public static MemoBuilder CognitivePatterns(this MemoBuilder builder, string value) =>
        builder.Field("c", "cp", "cognitive_patterns", value);

    public static MemoBuilder EvidenceQuotes(this MemoBuilder builder, string value) =>
        builder.Field("q", "ev", "evidence_quotes", value);

    public static MemoBuilder Technique(this MemoBuilder builder, string value) =>
        builder.Field("t", "tk", "technique", value);

    public static MemoBuilder SessionGoal(this MemoBuilder builder, string value) =>
        builder.Field("g", "sg", "session_goal", value);

    public static MemoBuilder RiskNote(this MemoBuilder builder, string value) =>
        builder.Field("n", "rn", "risk_note", value);

    public static MemoBuilder CrisisFlag(this MemoBuilder builder, string value) =>
        builder.Field("!", "cf", "crisis_flag", value);
}
