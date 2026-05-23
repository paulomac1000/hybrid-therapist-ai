namespace HybridTherapist.Domain.Models;

public sealed record MemorySummary(
    string Overview,
    IReadOnlyList<TopicEntry> TopicMap,
    string EmotionalArc,
    string? ClinicalFlags,
    string? FocusNext);

public sealed record TopicEntry(
    string Theme,
    string MessageRange,
    string Evolution,
    string Status);
