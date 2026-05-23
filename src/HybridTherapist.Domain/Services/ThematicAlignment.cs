namespace HybridTherapist.Domain.Services;

/// <summary>
/// Anti-hallucination guard. If the analyst's report mentions themes the user
/// never raised, null out the memo before it propagates to downstream layers.
/// Prevents the cascade: analyst fabricates "betrayal" → supervisor plans CBT for
/// trust issues → therapist tells the user about betrayal they never mentioned.
///
/// Cortexa parity: <c>CheckThematicAlignment</c> inside <c>TherapistFlow</c>.
/// </summary>
public static class ThematicAlignment
{
    public sealed record Result(bool Aligned, IReadOnlyList<string> UnsupportedThemes);

    // Themes that, if fabricated, are particularly harmful to insert into a session.
    // If the analyst mentions any of these and the user input doesn't, that's a misalignment.
    private static readonly (string Theme, string[] AnalystTokens, string[] UserSignals)[] SensitiveThemes =
    [
        ("betrayal", ["betrayal", "betrayed", "infidelit", "zdrad"], ["zdrad", "betray", "cheat", "affair"]),
        ("abuse", ["abuse", "abused", "abusive", "przemoc"], ["przemoc", "abuse", "krzywdz", "uderz", "biją", "bije", "hit"]),
        ("trauma", ["traumatic event", "PTSD", "trauma occurred"], ["trauma", "wypadek", "accident", "atak"]),
        ("suicide", ["suicidal", "ideation", "kill myself", "end my life"], ["samobój", "samobojstw", "skończyć", "skonczyc", "suicide", "kill myself", "koniec z"]),
        ("grief", ["bereavement", "grief over loss", "lost a loved one"], ["żałob", "zalob", "umarł", "umarl", "zmarł", "zmarl", "śmierć", "smierc", "loss", "passed away"]),
        ("addiction", ["addiction", "substance abuse", "alcoholism", "uzależnienie"], ["alkohol", "narkoty", "drugs", "addicted", "uzależn", "uzalezn"]),
        ("self_harm", ["self-harm", "selfharm", "cutting", "cut myself", "cięcie", "samookalecz", "okalecz", "żyletk"], ["samookalecz", "okalecz", "cięcie", "żyletk", "cut", "self-harm"]),
        ("eating_disorder", ["eating disorder", "anorexi", "bulimi", "binge", "purg", "zaburzenia odżywiania", "zaburzen odzywiania", "nie jem", "wymiot"], ["anoreks", "bulimi", "jedzeni", "wymiot", "nie jem", "głodz", "glodz", "eating"]),
        ("psychosis", ["psychosis", "psychotic", "hallucinat", "halucyn", "voices told me", "paranoi", "urojeni", "delusion"], ["halucyn", "głosy", "glosy", "urojeni", "paranoi", "śledz", "sledz", "psychoz"]),
    ];

    /// <summary>
    /// Returns <c>Aligned=false</c> with the list of fabricated themes if the analyst
    /// inserted a sensitive theme without supporting signal in the user input.
    /// </summary>
    public static Result Verify(string analystMemoOrReport, string userInput)
    {
        if (string.IsNullOrWhiteSpace(analystMemoOrReport) || string.IsNullOrWhiteSpace(userInput))
            return new Result(true, Array.Empty<string>());

        string analystLower = analystMemoOrReport.ToLowerInvariant();
        string userLower = userInput.ToLowerInvariant();

        var fabricated = new List<string>();
        foreach ((string theme, string[] analystTokens, string[] userSignals) in SensitiveThemes)
        {
            bool analystMentions = analystTokens.Any(t => analystLower.Contains(t, StringComparison.Ordinal));
            if (!analystMentions) continue;

            bool userSupports = userSignals.Any(s => userLower.Contains(s, StringComparison.Ordinal));
            if (!userSupports)
                fabricated.Add(theme);
        }

        return new Result(fabricated.Count == 0, fabricated);
    }
}
