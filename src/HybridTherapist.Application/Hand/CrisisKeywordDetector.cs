namespace HybridTherapist.Application.Hand;

/// <summary>
/// Lightweight Polish crisis keyword detector for the semantic safety net (Level 4).
/// Part of the therapeutic domain security.
/// </summary>
public static class CrisisKeywordDetector
{
    private static readonly (string pattern, int weight)[] CrisisPatterns =
    [
        ("samobój", 5),
        ("zabiję się", 5),
        ("nie chcę żyć", 5),
        ("okalecz", 3),
        ("skrzywdzę", 3),
        ("beznadziejne", 1),
        ("nie ma sensu", 2),
        ("koniec ze mną", 4),
    ];

    public static bool DetectCrisisKeywords(string text) => DetectCrisisKeywords(text, 7);

    public static bool DetectCrisisKeywords(string text, int threshold) =>
        CrisisPatterns
            .Where(p => text.Contains(p.pattern, StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.weight) >= threshold;
}
