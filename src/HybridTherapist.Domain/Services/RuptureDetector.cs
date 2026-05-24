using System.Text.RegularExpressions;

namespace HybridTherapist.Domain.Services;

/// <summary>
/// Detects "therapeutic rupture" — moments when the user signals that the previous
/// assistant response missed the mark. When a rupture fires, the flow forces
/// <see cref="Models.ResponseStrategy.Repair"/> regardless of phase/severity.
///
/// Cortexa parity: <c>Cortexa.Orchestrator.Domain.Services.Therapy.RuptureDetector</c>.
/// </summary>
public static partial class RuptureDetector
{
    public sealed record Result(bool Detected, string? Reason);

    // Polish + English correction signals. Case-insensitive.
    [GeneratedRegex(@"\b(nie|źle|zle|wcale|wręcz|wrecz|nie\s*to|nie\s*tak|nie\s*zrozumiał|zrozumial|nie\s*chodziło|chodzilo|nie\s*o\s*to|źle\s*mnie\s*rozumiesz|wrong|that.?s\s*not|no,?\s*i\s*don.?t|misunderstood|missed\s*the\s*point|not\s*what\s*i|jak\s+już\s+mówi|jak\s+juz\s+mowi|znowu\s+to\s+samo|znów\s+to\s+samo|znow\s+to\s+samo|nie\s+o\s+to\s+pytał|nie\s+o\s+to\s+pytal|nie\s+odpowiedział|nie\s+odpowiedzial|dalej\s+nie\s+rozumiesz|ignorujesz\s+mnie|powtarzasz\s+się|powtarzasz\s+sie|nic\s+nowego\s+nie\s+powiedział|nic\s+nowego\s+nie\s+powiedzial)\b",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 200)]
    private static partial Regex CorrectionSignals();

    [GeneratedRegex(@"(nie\s*słucha|nie\s*slucha|nie\s*pomag|don.?t\s*understand|you.?re\s*not\s*listening|stop\s*it|frustruj|annoying|ignor|to\s+nie\s+pomaga|męczy\s+mnie\s+to|meczy\s+mnie\s+to|bez\s+sensu\s+ta\s+rozmowa|gadam\s+jak\s+do\s+ściany|gadam\s+jak\s+do\s+sciany)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 200)]
    private static partial Regex FrustrationSignals();

    /// <summary>
    /// Checks the current user message against the most recent assistant message.
    /// Rupture fires when there's an explicit correction OR frustration signal AND
    /// there was a prior assistant turn (no rupture on first turn).
    /// </summary>
    public static Result Check(string userMessage, string? lastAssistantMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage)) return new Result(false, null);
        if (string.IsNullOrWhiteSpace(lastAssistantMessage)) return new Result(false, null);

        try
        {
            if (CorrectionSignals().IsMatch(userMessage))
                return new Result(true, "user_correction");

            if (FrustrationSignals().IsMatch(userMessage))
                return new Result(true, "user_frustration");
        }
        catch (RegexMatchTimeoutException)
        {
            // Fail open — treat as no rupture
        }

        return new Result(false, null);
    }
}
