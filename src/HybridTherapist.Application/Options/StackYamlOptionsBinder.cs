namespace HybridTherapist.Application.Options;

/// <summary>
/// Overlays <see cref="StackConfig"/> values onto <see cref="TherapistOptions"/>.
///
/// Stack.yaml is the source of truth when present; appsettings.json acts as fallback.
/// Supports both hybrid-therapist key names (translator/analyst/...) and cortexa's
/// stack.yaml conventions (bielik_translator_1_5b, therapy_analyst, ...) so the same
/// file works in both deployments unchanged.
///
/// Provider filtering: L1-L7 layer slots only accept Ollama-runnable entries; the
/// cloud fallback slot only accepts OpenRouter entries. This prevents picking a
/// cortexa "translator" key (which maps to cloud Gemini) for a local Bielik slot.
/// </summary>
public static class StackYamlOptionsBinder
{
    private const string OllamaProvider = "ollama";

    // Ollama-side roles. Aliases tried in order, first provider=ollama match wins.
    // All Socrates layers are LOCAL — no cloud anywhere in the chain.
    private static readonly Dictionary<string, string[]> OllamaRoleAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Translator"] = ["bielik_translator_1_5b", "translator"],
        ["Analyst"] = ["therapy_analyst", "analyst"],
        ["Supervisor"] = ["therapy_supervisor", "supervisor"],
        ["Therapist"] = ["therapist_core", "therapist_balanced", "therapist"],
        ["Calibrator"] = ["therapist_calibrator", "calibrator", "psychocounsel"],
    };

    public static void ApplyStackYaml(this TherapistOptions options, StackConfig stack)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(stack);

        options.Translator = Resolve(stack, "Translator", OllamaProvider, options.Translator);
        options.Analyst = Resolve(stack, "Analyst", OllamaProvider, options.Analyst);
        options.Supervisor = Resolve(stack, "Supervisor", OllamaProvider, options.Supervisor);
        options.Therapist = Resolve(stack, "Therapist", OllamaProvider, options.Therapist);
        options.Calibrator = Resolve(stack, "Calibrator", OllamaProvider, options.Calibrator);
    }

    private static string Resolve(StackConfig stack, string logicalRole, string requiredProvider, string fallback)
    {
        if (!OllamaRoleAliases.TryGetValue(logicalRole, out string[]? aliases))
            return fallback;

        foreach (string alias in aliases)
        {
            try
            {
                return stack.ResolveModelName(alias, requiredProvider);
            }
            catch (KeyNotFoundException)
            {
                // try next alias
            }
        }

        return fallback;
    }
}
