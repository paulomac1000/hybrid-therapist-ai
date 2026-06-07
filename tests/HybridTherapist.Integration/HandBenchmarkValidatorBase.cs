using System.Text.Json;
using FluentAssertions;

namespace HybridTherapist.Integration;

public abstract class HandBenchmarkValidatorBase
{
    protected static readonly string[] L4ForbiddenInstructionMarkers =
    {
        "Use the information below",
        "Read the M| messages",
        "structured clinical context",
        "Analyst memo keys",
        "Supervisor memo keys",
        "em=emotional state",
        "sv=severity",
        "ap=approach",
    };

    protected static readonly string[] EnglishMarkers =
    {
        "what ", "when ", "i hear", "i understand", "anxiety", "panic attack",
        "depression", "worry", "therapist", "you are", "tell me",
    };

    protected static readonly char[] Separators = { ' ', '\n', '\t', '.', ',', ';', ':', '!', '?', '(', ')', '"', '\'' };

    protected static readonly HashSet<string> PolishStopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "że", "nie", "się", "jest", "to", "jak", "co", "kiedy", "który", "która",
        "które", "twoje", "twoja", "twojego", "cię", "ciebie", "dla", "bez", "przed",
    };

    public static bool LooksPolish(string response)
    {
        if (string.IsNullOrWhiteSpace(response) || response.Trim().Length < 50)
            return false;

        string lower = response.ToLowerInvariant();
        int englishHits = EnglishMarkers.Count(marker => lower.Contains(marker, StringComparison.Ordinal));
        if (englishHits >= 2)
            return false;

        int letters = response.Count(char.IsLetter);
        int diacritics = response.Count(c => "ąćęłńóśźżĄĆĘŁŃÓŚŹŻ".Contains(c));
        bool hasDiacritics = letters > 0 && diacritics * 100.0 / letters >= 0.5;

        string[] words = lower.Split(
            Separators,
            StringSplitOptions.RemoveEmptyEntries);
        bool hasPolishStopword = words.Any(w => PolishStopwords.Contains(w));

        return hasDiacritics || hasPolishStopword;
    }

    private protected static async Task<BenchmarkExpectations> ReadExpectationsAsync(string cassettePath)
    {
        string cassetteJson = await File.ReadAllTextAsync(cassettePath);
        using JsonDocument cassetteDoc = JsonDocument.Parse(cassetteJson);
        JsonElement expected = cassetteDoc.RootElement.GetProperty("expected_quality");

        return new BenchmarkExpectations(
            UserInputPl: cassetteDoc.RootElement.GetProperty("user_input_pl").GetString()!,
            ExpectedPass: expected.GetProperty("pass").GetBoolean(),
            MinQualityScore: expected.GetProperty("min_quality_score").GetInt32(),
            RequiredTopics: ReadStringArray(expected, "required_topics"),
            RequiredPhrasesPl: ReadStringArray(expected, "required_phrases_pl"),
            ForbiddenPhrases: ReadStringArray(expected, "forbidden_phrases"));
    }

    private protected static BenchmarkMetadata ReadMetadata(JsonElement meta)
    {
        IReadOnlyList<string> topics = meta.TryGetProperty("topics", out JsonElement topicsEl)
            && topicsEl.ValueKind == JsonValueKind.Array
                ? topicsEl.EnumerateArray()
                    .Select(t => t.GetString())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t!)
                    .ToArray()
                : Array.Empty<string>();

        return new BenchmarkMetadata(
            SessionId: meta.GetProperty("session_id").GetString()!,
            Fallback: meta.GetProperty("fallback").GetBoolean(),
            CrisisDetected: meta.GetProperty("crisis_detected").GetBoolean(),
            Phase: meta.GetProperty("phase").GetString()!,
            SupervisorApproach: meta.TryGetProperty("supervisor_approach", out JsonElement sa) ? sa.GetString() : null,
            Topics: topics);
    }

    private protected static BenchmarkTraceEvent ReadTraceEvent(JsonElement evt) => new(
        Layer: evt.GetProperty("layer").GetString()!,
        Input: evt.GetProperty("input").GetString() ?? string.Empty,
        Output: evt.GetProperty("output").GetString() ?? string.Empty,
        WireFormat: evt.TryGetProperty("wire_format", out JsonElement wf) && wf.ValueKind != JsonValueKind.Null
            ? wf.GetString()
            : null,
        Outcome: evt.GetProperty("outcome").GetString() ?? string.Empty);

    protected static string[] ReadStringArray(JsonElement root, string propertyName) =>
        root.GetProperty(propertyName).EnumerateArray().Select(e => e.GetString()!).ToArray();

    private protected static BenchmarkTraceEvent SingleLayer(BenchmarkRun run, string layer)
    {
        run.Events.Count(e => e.Layer == layer).Should().Be(1, $"trace must contain exactly one {layer} event");
        return run.Events.Single(e => e.Layer == layer);
    }

    private protected static string WireOrOutput(BenchmarkTraceEvent evt, string label)
    {
        string wire = string.IsNullOrWhiteSpace(evt.WireFormat) ? evt.Output : evt.WireFormat!;
        wire.Should().NotBeNullOrWhiteSpace($"{label} event must expose wire_format or output");
        return wire;
    }

    private protected static void ValidateExpectedQuality(BenchmarkRun run, BenchmarkExpectations expected)
    {
        expected.ExpectedPass.Should().BeTrue("hand benchmark cassettes are strict passing scenarios");

        foreach (string topic in expected.RequiredTopics)
            run.Metadata.Topics.Should().Contain(topic, $"required topic '{topic}' must be present in metadata.topics");

        foreach (string phrase in expected.RequiredPhrasesPl)
        {
            run.Content.Contains(phrase, StringComparison.OrdinalIgnoreCase)
                .Should().BeTrue($"final Polish response must contain required phrase '{phrase}'");
        }

        foreach (string phrase in expected.ForbiddenPhrases)
        {
            run.Content.Contains(phrase, StringComparison.OrdinalIgnoreCase)
                .Should().BeFalse($"final Polish response must not contain forbidden phrase '{phrase}'");
        }

        LooksPolish(run.Content).Should().BeTrue("final response must look Polish, not mostly English");
        run.Content.Should().Contain("?", "therapist contract requires an open question to continue");
    }

    protected static void ValidateL4ForbiddenMarkers(string input)
    {
        foreach (string marker in L4ForbiddenInstructionMarkers)
            input.Should().NotContain(marker, $"L4 prompt must not contain format instruction marker '{marker}'");
    }

    protected static void AssertAllowedKeysOnly(string wire, ISet<string> allowedKeys, string label)
    {
        foreach (string part in wire.Split('|').Skip(1))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0) continue;
            string key = part[..eq];
            allowedKeys.Should().Contain(key, $"{label} must not contain fallback or out-of-codec key '{key}'");
        }
    }

    protected static int CountTokenEquivalents(string text) =>
        (int)Math.Ceiling(text.Length / 3.5);
}

internal sealed record BenchmarkExpectations(
    string UserInputPl,
    bool ExpectedPass,
    int MinQualityScore,
    IReadOnlyList<string> RequiredTopics,
    IReadOnlyList<string> RequiredPhrasesPl,
    IReadOnlyList<string> ForbiddenPhrases);

internal sealed record BenchmarkRun(
    string Content,
    BenchmarkMetadata Metadata,
    IReadOnlyList<BenchmarkTraceEvent> Events);

internal sealed record BenchmarkMetadata(
    string SessionId,
    bool Fallback,
    bool CrisisDetected,
    string Phase,
    string? SupervisorApproach,
    IReadOnlyList<string> Topics);

internal sealed record BenchmarkTraceEvent(
    string Layer,
    string Input,
    string Output,
    string? WireFormat,
    string Outcome);

internal sealed record TokenSavingsMetrics(
    int WireTokens,
    int PlaintextTokens,
    int TokensSaved,
    double SavingsPercent);
