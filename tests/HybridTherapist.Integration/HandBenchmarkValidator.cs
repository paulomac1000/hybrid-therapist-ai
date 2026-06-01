using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HybridTherapist.Application.Hand;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace HybridTherapist.Integration;

internal static class HandBenchmarkValidator
{
    private static readonly string[] OldKeys = { "em=", "sv=", "ri=", "cp=", "ap=", "tk=", "kq=", "rn=", "sg=", "cf=" };
    private static readonly string[] VerboseKeys =
    {
        "emotional_state", "severity", "risk_indicators", "cognitive_patterns",
        "approach", "technique", "key_question", "risk_note", "session_goal", "crisis_flag",
    };
    private static readonly string[] L4ForbiddenInstructionMarkers =
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
    private static readonly string[] EnglishMarkers =
    {
        "what ", "when ", "i hear", "i understand", "anxiety", "panic attack",
        "depression", "worry", "therapist", "you are", "tell me",
    };
    private static readonly HashSet<string> PolishStopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "że", "nie", "się", "jest", "to", "jak", "co", "kiedy", "który", "która",
        "które", "twoje", "twoja", "twojego", "cię", "ciebie", "dla", "bez", "przed",
    };

    public static async Task<(BenchmarkRun Run, BenchmarkExpectations Expectations)> RunCassetteAsync(string cassettePath)
    {
        BenchmarkExpectations expectations = await ReadExpectationsAsync(cassettePath);
        await using CassetteOllamaServer ollama = await CassetteOllamaServer.StartAsync(cassettePath);

        await using WebApplicationFactory<Program> app = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Ollama:BaseUrl"] = ollama.BaseUrl,
                    });
                });
            });

        HttpClient client = app.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/v1/chat/completions", new
        {
            model = "hybrid-therapist",
            messages = new[] { new { role = "user", content = expectations.UserInputPl } },
        });

        response.EnsureSuccessStatusCode();
        using JsonDocument resultDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string content = resultDoc.RootElement.GetProperty("choices")[0]
            .GetProperty("message").GetProperty("content").GetString()!;
        BenchmarkMetadata metadata = ReadMetadata(resultDoc.RootElement.GetProperty("metadata"));

        HttpResponseMessage traceResp = await client.GetAsync($"/v1/trace/{metadata.SessionId}");
        traceResp.EnsureSuccessStatusCode();
        using JsonDocument traceDoc = JsonDocument.Parse(await traceResp.Content.ReadAsStringAsync());
        var events = traceDoc.RootElement.GetProperty("events")
            .EnumerateArray()
            .Select(ReadTraceEvent)
            .ToList();

        return (new BenchmarkRun(content, metadata, events), expectations);
    }

    public static void ValidateStrict(BenchmarkRun run, BenchmarkExpectations expected)
    {
        run.Metadata.Fallback.Should().BeFalse("strict H.A.N.D. benchmark must not pass through fallback");

        BenchmarkTraceEvent l2 = SingleLayer(run, "L2_analyst");
        BenchmarkTraceEvent l3 = SingleLayer(run, "L3_supervisor");
        BenchmarkTraceEvent l4 = SingleLayer(run, "L4_therapist");

        l2.Outcome.Should().Be("ok", "L2 must successfully emit native Codec G, not error fallback");
        l3.Outcome.Should().Be("ok", "L3 must successfully emit native Codec G, not error fallback");

        string l2Wire = WireOrOutput(l2, "L2");
        string l3Wire = WireOrOutput(l3, "L3");

        ValidateL2Wire(l2Wire);
        ValidateL3Wire(l3Wire);
        ValidateL4Input(l4.Input);
        ValidateExpectedQuality(run, expected);
    }

    public static TokenSavingsMetrics CalculateTokenSavings(BenchmarkRun run)
    {
        string l2Wire = WireOrOutput(SingleLayer(run, "L2_analyst"), "L2");
        string l3Wire = WireOrOutput(SingleLayer(run, "L3_supervisor"), "L3");
        int wireTokens = CountTokenEquivalents(l2Wire + "\n" + l3Wire);

        TokenSavingsTracker.StrictCodecG = true;
        try
        {
            string plaintext = TokenSavingsTracker.ExpandMemoToPlaintext(l2Wire)
                + "\n"
                + TokenSavingsTracker.ExpandMemoToPlaintext(l3Wire);
            int plaintextTokens = CountTokenEquivalents(plaintext);
            double savingsPercent = plaintextTokens > 0
                ? Math.Round((1.0 - (double)wireTokens / plaintextTokens) * 100, 1)
                : 0;
            return new TokenSavingsMetrics(wireTokens, plaintextTokens, plaintextTokens - wireTokens, savingsPercent);
        }
        finally
        {
            TokenSavingsTracker.StrictCodecG = false;
        }
    }

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
            new[] { ' ', '\n', '\t', '.', ',', ';', ':', '!', '?', '(', ')', '"', '\'' },
            StringSplitOptions.RemoveEmptyEntries);
        bool hasPolishStopword = words.Any(w => PolishStopwords.Contains(w));

        return hasDiacritics || hasPolishStopword;
    }

    private static async Task<BenchmarkExpectations> ReadExpectationsAsync(string cassettePath)
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

    private static BenchmarkMetadata ReadMetadata(JsonElement meta)
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

    private static BenchmarkTraceEvent ReadTraceEvent(JsonElement evt) => new(
        Layer: evt.GetProperty("layer").GetString()!,
        Input: evt.GetProperty("input").GetString() ?? string.Empty,
        Output: evt.GetProperty("output").GetString() ?? string.Empty,
        WireFormat: evt.TryGetProperty("wire_format", out JsonElement wf) && wf.ValueKind != JsonValueKind.Null
            ? wf.GetString()
            : null,
        Outcome: evt.GetProperty("outcome").GetString() ?? string.Empty);

    private static string[] ReadStringArray(JsonElement root, string propertyName) =>
        root.GetProperty(propertyName).EnumerateArray().Select(e => e.GetString()!).ToArray();

    private static BenchmarkTraceEvent SingleLayer(BenchmarkRun run, string layer)
    {
        run.Events.Count(e => e.Layer == layer).Should().Be(1, $"trace must contain exactly one {layer} event");
        return run.Events.Single(e => e.Layer == layer);
    }

    private static string WireOrOutput(BenchmarkTraceEvent evt, string label)
    {
        string wire = string.IsNullOrWhiteSpace(evt.WireFormat) ? evt.Output : evt.WireFormat!;
        wire.Should().NotBeNullOrWhiteSpace($"{label} event must expose wire_format or output");
        return wire;
    }

    private static void ValidateL2Wire(string wire)
    {
        wire.Should().StartWith("M|L=2|", "L2 must emit M|L=2| wire format");
        wire.Should().Contain("e7=", "L2 must contain e7= Codec G emotional-state key");
        wire.Should().Contain("s9=", "L2 must contain s9= Codec G severity key");
        (wire.Contains("x4=", StringComparison.Ordinal)
            || wire.Contains("y1=", StringComparison.Ordinal)
            || wire.Contains("q3=", StringComparison.Ordinal))
            .Should().BeTrue("L2 must contain at least one additional Codec G key (x4/y1/q3)");
        AssertNoForbiddenKeys(wire, "L2");
        AssertAllowedKeysOnly(wire, new HashSet<string>(StringComparer.Ordinal) { "L", "e7", "s9", "x4", "y1", "q3" }, "L2");
    }

    private static void ValidateL3Wire(string wire)
    {
        wire.Should().StartWith("M|L=3|", "L3 must emit M|L=3| wire format");
        wire.Should().Contain("p3=", "L3 must contain p3= Codec G approach key");
        wire.Should().Contain("t5=", "L3 must contain t5= Codec G technique key");
        wire.Should().Contain("k2=", "L3 must contain k2= Codec G key-question key");
        AssertNoForbiddenKeys(wire, "L3");
        AssertAllowedKeysOnly(wire, new HashSet<string>(StringComparer.Ordinal) { "L", "p3", "t5", "k2", "r8", "g6", "f0" }, "L3");
    }

    private static void ValidateL4Input(string input)
    {
        input.Should().Contain("M|L=2|", "L4 must receive raw L2 Codec G memo");
        input.Should().Contain("e7=", "L4 raw L2 memo must contain e7=");
        input.Should().Contain("s9=", "L4 raw L2 memo must contain s9=");
        input.Should().Contain("M|L=3|", "L4 must receive raw L3 Codec G memo");
        input.Should().Contain("p3=", "L4 raw L3 memo must contain p3=");
        input.Should().Contain("t5=", "L4 raw L3 memo must contain t5=");
        input.Should().Contain("k2=", "L4 raw L3 memo must contain k2=");

        foreach (string marker in L4ForbiddenInstructionMarkers)
            input.Should().NotContain(marker, $"L4 prompt must not contain format instruction marker '{marker}'");
    }

    private static void ValidateExpectedQuality(BenchmarkRun run, BenchmarkExpectations expected)
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

    private static void AssertNoForbiddenKeys(string wire, string label)
    {
        foreach (string oldKey in OldKeys)
            wire.Should().NotContain(oldKey, $"{label} must not contain old key '{oldKey}'");
        foreach (string verboseKey in VerboseKeys)
            wire.Should().NotContain(verboseKey, $"{label} must not contain verbose key '{verboseKey}'");
    }

    private static void AssertAllowedKeysOnly(string wire, ISet<string> allowedKeys, string label)
    {
        foreach (string part in wire.Split('|').Skip(1))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0) continue;
            string key = part[..eq];
            allowedKeys.Should().Contain(key, $"{label} must not contain fallback or out-of-codec key '{key}'");
        }
    }

    private static int CountTokenEquivalents(string text) =>
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
