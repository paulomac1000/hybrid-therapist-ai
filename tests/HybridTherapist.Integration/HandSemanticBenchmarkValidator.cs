using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HybridTherapist.Application.Hand;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using HybridTherapist.Application.Options;

namespace HybridTherapist.Integration;

internal static class HandSemanticBenchmarkValidator
{
    private static readonly string[] CompactKeys = { "e7=", "s9=", "x4=", "y1=", "q3=", "p3=", "t5=", "k2=", "r8=", "g6=", "f0=" };
    
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
                        ["Models:HandWireVariant"] = "Semantic",
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

    public static void ValidateSemanticStrict(BenchmarkRun run, BenchmarkExpectations expected)
    {
        run.Metadata.Fallback.Should().BeFalse("strict H.A.N.D. benchmark must not pass through fallback");

        BenchmarkTraceEvent l2 = SingleLayer(run, "L2_analyst");
        BenchmarkTraceEvent l3 = SingleLayer(run, "L3_supervisor");
        BenchmarkTraceEvent l4 = SingleLayer(run, "L4_therapist");

        l2.Outcome.Should().Be("ok", "L2 must successfully emit Semantic H.A.N.D. wire, not fallback");
        l3.Outcome.Should().Be("ok", "L3 must successfully emit Semantic H.A.N.D. wire, not fallback");

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

        string l2Compact = l2Wire.Replace("em=", "e7=").Replace("sv=", "s9=").Replace("ri=", "x4=").Replace("cp=", "y1=").Replace("ev=", "q3=");
        string l3Compact = l3Wire.Replace("ap=", "p3=").Replace("tk=", "t5=").Replace("kq=", "k2=").Replace("rn=", "r8=").Replace("sg=", "g6=").Replace("cf=", "f0=");

        TokenSavingsTracker.StrictCodecG = true;
        try
        {
            string plaintext = TokenSavingsTracker.ExpandMemoToPlaintext(l2Compact)
                + "\n"
                + TokenSavingsTracker.ExpandMemoToPlaintext(l3Compact);
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
        wire.Should().Contain("em=", "L2 must contain em= semantic key");
        wire.Should().Contain("sv=", "L2 must contain sv= semantic key");
        AssertNoCompactKeys(wire, "L2");
        AssertAllowedKeysOnly(wire, new HashSet<string>(StringComparer.Ordinal) { "L", "em", "sv", "ri", "cp", "ev" }, "L2");
    }

    private static void ValidateL3Wire(string wire)
    {
        wire.Should().StartWith("M|L=3|", "L3 must emit M|L=3| wire format");
        wire.Should().Contain("ap=", "L3 must contain ap= semantic key");
        wire.Should().Contain("tk=", "L3 must contain tk= semantic key");
        wire.Should().Contain("kq=", "L3 must contain kq= semantic key");
        AssertNoCompactKeys(wire, "L3");
        AssertAllowedKeysOnly(wire, new HashSet<string>(StringComparer.Ordinal) { "L", "ap", "tk", "kq", "rn", "sg", "cf" }, "L3");
    }

    private static void ValidateL4Input(string input)
    {
        input.Should().Contain("M|L=2|", "L4 must receive raw L2 Semantic memo");
        input.Should().Contain("em=", "L4 raw L2 memo must contain em=");
        input.Should().Contain("sv=", "L4 raw L2 memo must contain sv=");
        input.Should().Contain("M|L=3|", "L4 must receive raw L3 Semantic memo");
        input.Should().Contain("ap=", "L4 raw L3 memo must contain ap=");
        input.Should().Contain("tk=", "L4 raw L3 memo must contain tk=");
        input.Should().Contain("kq=", "L4 raw L3 memo must contain kq=");

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

        HandBenchmarkValidator.LooksPolish(run.Content).Should().BeTrue("final response must look Polish, not mostly English");
        run.Content.Should().Contain("?", "therapist contract requires an open question to continue");
    }

    private static void AssertNoCompactKeys(string wire, string label)
    {
        foreach (string compKey in CompactKeys)
            wire.Should().NotContain(compKey, $"{label} must not contain compact key '{compKey}'");
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
