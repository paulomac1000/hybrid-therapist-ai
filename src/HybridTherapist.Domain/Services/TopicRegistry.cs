namespace HybridTherapist.Domain.Services;

/// <summary>
/// Heuristic topic extractor for Polish therapy input. Detects common therapy themes
/// (sleep, anxiety, relationships, work, etc.) from the user's message and tracks them
/// across the session. Used by <c>CheckThematicAlignment</c> to prevent the analyst from
/// fabricating themes the user never raised.
///
/// Cortexa parity: <c>Cortexa.Orchestrator.Application.Services.TopicRegistry</c>.
/// </summary>
public static class TopicRegistry
{
    // Polish + English keyword → canonical topic id. Multiple keywords can map to one topic.
    private static readonly (string Topic, string[] Keywords)[] TopicMap =
    [
        ("sleep", ["zasnąć", "zasypia", "zasnac", "bezsenność", "bezsennosc", "śpię", "śpij", "spie", "sen ", "sleep", "insomnia", "wybudz", "budzę"]),
        ("anxiety", ["lęk", "lek ", "niepokój", "niepokoj", "boję się", "boje sie", "anxiety", "panic", "panika", "stres", "stress", "zdenerwowany", "zdenerwowana"]),
        ("depression", ["depresj", "smutek", "smutn", "przygnębi", "przygnebi", "beznadziej", "bez sensu", "depress", "down", "empty"]),
        ("relationships", ["partner", "mąż", "maz", "żona", "zona", "chłopak", "chlopak", "dziewczyn", "związek", "zwiazek", "relacj", "rodzin", "rozwód", "rozwod", "zdrada", "betrayal", "marriage"]),
        ("work", ["praca", "robota", "szef", "kolega", "koledzy", "wypal", "burnout", "kariera", "job", "boss", "work"]),
        ("loneliness", ["samotn", "lonely", "alone", "izolac", "nie mam nikogo"]),
        ("anger", ["złość", "zlosc", "wściek", "wsciek", "wkurz", "frustracj", "anger", "angry", "irrit"]),
        ("grief", ["żałob", "zalob", "strat", "zmarł", "zmarl", "śmierć", "smierc", "po stracie", "grief", "mourning", "loss"]),
        ("trauma", ["trauma", "krzywd", "abuse", "molestow", "gwałt", "gwalt", "przemoc"]),
        ("self_worth", ["wartość", "wartosc", "bezwartoś", "bezwartos", "nie zasługuj", "nie zasluguj", "self-worth", "worth", "useless"]),
        ("health", ["choroba", "choruj", "lekarz", "ból", "bol ", "headache", "migrena", "illness", "diagnoz"]),
        ("future", ["przyszłoś", "przyszlos", "future", "plany", "co dalej", "co teraz"]),
    ];

    /// <summary>
    /// Extracts canonical topic ids from text. Matching is case-insensitive, substring-based.
    /// Returns deduplicated topics in order of first appearance.
    /// </summary>
    public static IReadOnlyList<string> ExtractTopics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();

        string lower = text.ToLowerInvariant();
        var found = new List<string>();
        foreach ((string topic, string[] keywords) in TopicMap)
        {
            foreach (string kw in keywords)
            {
                if (lower.Contains(kw, StringComparison.Ordinal))
                {
                    if (!found.Contains(topic, StringComparer.Ordinal))
                        found.Add(topic);
                    break;
                }
            }
        }
        return found;
    }

    /// <summary>
    /// Returns active topics for a session — union of the existing topic list
    /// and topics newly detected in the latest user message.
    /// </summary>
    public static IReadOnlyList<string> Merge(IEnumerable<string> existing, IEnumerable<string> fresh)
    {
        var result = new List<string>();
        foreach (string t in existing) if (!result.Contains(t, StringComparer.Ordinal)) result.Add(t);
        foreach (string t in fresh) if (!result.Contains(t, StringComparer.Ordinal)) result.Add(t);
        return result;
    }
}
