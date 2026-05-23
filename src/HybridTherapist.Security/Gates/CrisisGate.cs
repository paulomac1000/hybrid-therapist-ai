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
        @"(nie\s+śpię|nie\s+mogę\s+zasnąć|budzę\s+się\s+w\s+nocy|bezsenność|kołatanie\s+serca|ciągle\s+zmęczony|ciągle\s+zmęczona|nie\s+sypiam|problemy\s+ze\s+snem|nie\s+wysypiam\s+się)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 200)]
    private static partial Regex MediumSeverityPl();

    // Anhedonia / depression markers (high)
    [GeneratedRegex(
        @"(nic\s+nie\s+sprawia.*przyjemności|nic\s+mnie.*nie\s+cieszy|stracił[ae]m?\s+zainteresowanie|stracił[ae]m?\s+radość|wszystko\s+jest.*obojętne|nie\s+czuję\s+nic|czuję\s+pustkę|(?:życie|wszystko)\s+jest\s+bez\s+sensu|nie\s+mam\s+siły\s+żyć|wypalon)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 200)]
    private static partial Regex AnhedoniaPl();

    // Social withdrawal (moderate)
    [GeneratedRegex(
        @"(nikt\s+mnie\s+nie\s+rozumie|czuję\s+się\s+sam|czuje\s+sie\s+sam|wyobcowan|izoluj|unikam\s+ludzi|nie\s+mam\s+do\s+kogo|samotno)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 200)]
    private static partial Regex SocialWithdrawalPl();

    // Panic / anxiety (high)
    [GeneratedRegex(
        @"(nie\s+mogę\s+oddychać|serce.*w\s+piersi|duszę\s+się|atak[yi]\s+paniki|ściska.*w\s+klatce|niepokój.*nie\s+daje|ataki\s+lęku|napady\s+lęku|ciągły\s+niepokój|ciagly\s+niepokoj)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 200)]
    private static partial Regex PanicAnxietyPl();

    // Anger / irritability (moderate)
    [GeneratedRegex(
        @"(wkurza.*mnie|denerwuje|mnie\s+denerwuje|nie\s+wytrzymuję|mam\s+dość|wszystko\s+mnie\s+drażni|jestem\s+wściek|jestem\s+wsciek|nie\s+mogę\s+się\s+uspokoić)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 200)]
    private static partial Regex AngerPl();

    // Cognitive complaints (moderate)
    [GeneratedRegex(
        @"(nie\s+mogę\s+się\s+skupić|zapominam|pustka\s+w\s+głowie|nie\s+myślę\s+jasno|mg[łl][aąeę]\s+m[oó]zgow[aąeę]|nie\s+mogę\s+się\s+skoncentrować|rozkojarzon)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 200)]
    private static partial Regex CognitivePl();

    // Insomnia extended (moderate)
    [GeneratedRegex(
        @"(problemy\s+ze\s+snem|nie\s+przesypiam|budzę\s+się\s+o\s+trzeciej|wstaję\s+zmęczon|nie\s+wysypiam|koszmary|koszmarów|wybudzam\s+się)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 200)]
    private static partial Regex InsomniaExtendedPl();

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

            if (AnhedoniaPl().IsMatch(input) || PanicAnxietyPl().IsMatch(input))
                return CrisisGateResult.Escalation("high");

            if (SocialWithdrawalPl().IsMatch(input) || AngerPl().IsMatch(input) ||
                CognitivePl().IsMatch(input) || InsomniaExtendedPl().IsMatch(input))
                return CrisisGateResult.Escalation("moderate");

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
