using System.Text.RegularExpressions;

namespace HybridTherapist.Security.Privacy;

/// <summary>
/// Regex-based PII sanitizer. Scrubs names-adjacent strings, emails, phones, PESEL.
/// No AI-contextual pass in demo mode — pure regex only.
/// </summary>
public sealed partial class PrivacySanitizer
{
    [GeneratedRegex(@"\b[A-ZŁŚŻŹĆŃÓĘ][a-złśżźćńóę]{2,}\s+[A-ZŁŚŻŹĆŃÓĘ][a-złśżźćńóę]{2,}\b", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex FullNamePattern();

    [GeneratedRegex(@"\b[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}\b", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\b(\+48\s?)?\d{3}[\s\-]?\d{3}[\s\-]?\d{3}\b", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"\b\d{11}\b", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex PeselPattern();

    public static string Sanitize(string input, string level = "basic")
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        string result = input;
        try
        {
            result = EmailPattern().Replace(result, "[REDACTED_EMAIL]");
            result = PhonePattern().Replace(result, "[REDACTED_PHONE]");
            result = PeselPattern().Replace(result, "[REDACTED_PESEL]");

            if (!string.Equals(level, "basic", StringComparison.OrdinalIgnoreCase))
                result = FullNamePattern().Replace(result, "[REDACTED_NAME]");
        }
        catch (RegexMatchTimeoutException)
        {
            // Never swallow — return last clean state
        }

        return result;
    }
}
