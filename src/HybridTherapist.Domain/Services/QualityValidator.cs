using System.Text.RegularExpressions;

namespace HybridTherapist.Domain.Services;

/// <summary>
/// Stage-2 QA — runs AFTER the calibrator and BEFORE the EN→PL translator.
/// Catches common failure modes that slip past per-layer parsing:
/// echo (response repeats user input), too short, wrong language, placeholder leak.
/// Cortexa parity: <c>Cortexa.Domain.Services.QualityValidator</c>.
/// </summary>
public static class QualityValidator
{
    private static readonly Regex AdviceRegex = new(
        @"\bmożesz\s+\p{L}", RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex TryRegex = new(
        @"\btry\b", RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));
    public sealed record Verdict(bool Ok, string Reason);

    /// <summary>
    /// Validates an English calibrator output before it goes to L7 translator.
    /// </summary>
    public static Verdict ValidateTherapeuticQuality(string response, string phase, int messageCount)
    {
        if (string.IsNullOrWhiteSpace(response))
            return new Verdict(false, "empty");

        string trimmed = response.Trim();

        bool containsQuestion = trimmed.Contains('?', StringComparison.Ordinal);
        bool containsAdvice =
            trimmed.Contains("spróbuj", StringComparison.OrdinalIgnoreCase) ||
            AdviceRegex.IsMatch(trimmed) ||
            trimmed.Contains("spróbować", StringComparison.OrdinalIgnoreCase) ||
            TryRegex.IsMatch(trimmed) ||
            trimmed.Contains("you can", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("it may help", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("one small step", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("warto", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("proponuję", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("proponuje", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("pomocne może być", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("jednym ze sposobów", StringComparison.OrdinalIgnoreCase);

        bool containsFormulaicOpening =
            trimmed.StartsWith("Rozumiem", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Widzę", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Słyszę", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("I understand", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("I see", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("I hear", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("It seems", StringComparison.OrdinalIgnoreCase);

        if (messageCount >= 4 && containsQuestion && !containsAdvice)
            return new Verdict(false, "only_questions_after_4_messages");

        if (containsFormulaicOpening)
            return new Verdict(false, "formulaic_opening");

        return new Verdict(true, "ok");
    }

    public static Verdict ValidateEnglishDraft(string draft, string userTextEn)
    {
        if (string.IsNullOrWhiteSpace(draft))
            return new Verdict(false, "empty_draft");

        string trimmed = draft.Trim();

        if (trimmed.Length < 20)
            return new Verdict(false, "too_short");

        int wordCount = trimmed.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount < 5)
            return new Verdict(false, "too_few_words");

        if (LooksLikeEcho(trimmed, userTextEn))
            return new Verdict(false, "echo_detected");

        if (ContainsPromptLeakage(trimmed))
            return new Verdict(false, "prompt_leakage");

        return new Verdict(true, "ok");
    }

    /// <summary>
    /// Validates the final Polish output before returning to the user.
    /// </summary>
    public static Verdict ValidatePolishOutput(string polish, string userTextPl)
    {
        if (string.IsNullOrWhiteSpace(polish))
            return new Verdict(false, "empty_output");

        string trimmed = polish.Trim();

        if (trimmed.Length < 15)
            return new Verdict(false, "too_short");

        if (LooksLikeEcho(trimmed, userTextPl))
            return new Verdict(false, "echo_detected");

        if (ContainsPromptLeakage(trimmed))
            return new Verdict(false, "prompt_leakage");

        // Polish output must contain some Polish characters
        int diacritics = 0;
        foreach (char c in trimmed)
            if ("ąćęłńóśźżĄĆĘŁŃÓŚŹŻ".Contains(c, StringComparison.Ordinal))
                diacritics++;
        if (trimmed.Length > 40 && diacritics == 0)
            return new Verdict(false, "not_polish");

        return new Verdict(true, "ok");
    }

    private static bool LooksLikeEcho(string response, string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput) || userInput.Length < 5) return false;
        string r = response.Trim().ToLowerInvariant();
        string u = userInput.Trim().ToLowerInvariant();
        // Exact echo
        if (r == u) return true;
        // Response is mostly the user input verbatim
        if (r.Length < u.Length * 2 && r.Contains(u, StringComparison.Ordinal)) return true;
        return false;
    }

    private static bool ContainsPromptLeakage(string text)
    {
        ReadOnlySpan<string> leakageTokens =
        [
            "confidence_decimal",
            "YOUR_TRANSLATION",
            "YOUR_ANSWER",
            "your_answer",
            "your_translation",
            "<answer>",
            "<translation>",
            "[ANALYST CONTEXT]",
            "[SUPERVISOR GUIDANCE]",
            "[THERAPIST DRAFT]",
            "Original user message (Polish):",
            "English therapeutic response to translate",
        ];

        foreach (string token in leakageTokens)
            if (text.Contains(token, StringComparison.Ordinal))
                return true;

        return false;
    }
}
