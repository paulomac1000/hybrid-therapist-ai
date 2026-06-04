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

    public static MemoBuilder Technique(this MemoBuilder builder, string value) =>
        builder.Field("t5", "t5", "technique", value);

    public static MemoBuilder RiskNote(this MemoBuilder builder, string value) =>
        builder.Field("r8", "r8", "risk_note", value);
}
