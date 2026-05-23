using System.Text.RegularExpressions;
using HybridTherapist.Domain.Interfaces;

namespace HybridTherapist.Security.Gates;

/// <summary>
/// Regex-based crisis detection gate. Must execute BEFORE any LLM call (safety layer -1).
/// Patterns are sourced from crisis_flow.yaml (Polish therapy user base).
/// </summary>
public sealed partial class CrisisGate : ICrisisGate
{
    private const string HardStopMessage =
        "Jest mi przykro, że przechodzisz przez trudne chwile. Jako asystent AI nie mogę udzielić " +
        "pomocy psychologicznej. Skontaktuj się z profesjonalistą: Telefon Zaufania dla Osób Dorosłych: " +
        "116 123 (bezpłatny, czynny 14:00-22:00) lub 112 w sytuacji zagrożenia życia.";

    // Hard-stop patterns (crisis_flow.yaml: suicide_direct_pl + suicide_direct_en)
    [GeneratedRegex(
        @"(samob[oó][jy]stw|zabi[ćjł]|umrze[ćj]|sko[ńn]cz[ęy][ćc]? z sob|jestem beznadziej)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 200)]
    private static partial Regex HardStopPl();

    [GeneratedRegex(
        @"(suicide|kill myself|end my life|want to die|hopeless|worthless)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 200)]
    private static partial Regex HardStopEn();

    // High severity — non-hard-stop escalations
    [GeneratedRegex(
        @"(nie\s+daję\s+rady|nie\s+wytrzymam|już\s+nie\s+mogę|załamał[aem]\s+się|przytłoczon[ay]|nie\s+widzę\s+wyjścia)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 200)]
    private static partial Regex HighSeverityPl();

    // Medium severity
    [GeneratedRegex(
        @"(nie\s+śpię|nie\s+mogę\s+zasnąć|budzę\s+się\s+w\s+nocy|bezsenność|kołatanie\s+serca|ciągle\s+zmęczony|ciągle\s+zmęczona)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 200)]
    private static partial Regex MediumSeverityPl();

    public CrisisGateResult Check(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return CrisisGateResult.Safe;

        try
        {
            if (HardStopPl().IsMatch(input) || HardStopEn().IsMatch(input))
                return CrisisGateResult.HardStop(HardStopMessage);

            if (HighSeverityPl().IsMatch(input))
                return CrisisGateResult.Escalation("high");

            if (MediumSeverityPl().IsMatch(input))
                return CrisisGateResult.Escalation("medium");
        }
        catch (RegexMatchTimeoutException)
        {
            // Never swallow — fail safe (allow flow to continue)
        }

        return CrisisGateResult.Safe;
    }
}
