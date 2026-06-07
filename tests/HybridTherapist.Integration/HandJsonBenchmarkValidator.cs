using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HybridTherapist.Application.Hand;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using HybridTherapist.Application.Options;

namespace HybridTherapist.Integration;

internal static class HandJsonBenchmarkValidator
{
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
                        ["Models:HandWireVariant"] = "Json",
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

    public static void ValidateJsonStrict(BenchmarkRun run, BenchmarkExpectations expected)
    {
        run.Metadata.Fallback.Should().BeFalse("strict JSON benchmark must not pass through fallback");

        BenchmarkTraceEvent l2 = SingleLayer(run, "L2_analyst");
        BenchmarkTraceEvent l3 = SingleLayer(run, "L3_supervisor");
        BenchmarkTraceEvent l4 = SingleLayer(run, "L4_therapist");

        l2.Outcome.Should().Be("ok", "L2 must successfully emit JSON, not error fallback");
        l3.Outcome.Should().Be("ok", "L3 must successfully emit JSON, not error fallback");

        string l2Wire = WireOrOutput(l2, "L2");
        string l3Wire = WireOrOutput(l3, "L3");

        l2Wire.Should().NotStartWith("M|", "JSON L2 must not emit wire format");
        l3Wire.Should().NotStartWith("M|", "JSON L3 must not emit wire format");

        // Parse L2 JSON
        using (JsonDocument l2Doc = JsonDocument.Parse(l2Wire))
        {
            var l2Root = l2Doc.RootElement;
            l2Root.TryGetProperty("emotional_state", out var p1).Should().BeTrue("JSON L2 must have emotional_state");
            p1.GetString().Should().NotBeNullOrWhiteSpace();
            l2Root.TryGetProperty("severity", out var p2).Should().BeTrue("JSON L2 must have severity");
            p2.GetString().Should().NotBeNullOrWhiteSpace();
        }

        // Parse L3 JSON
        using (JsonDocument l3Doc = JsonDocument.Parse(l3Wire))
        {
            var l3Root = l3Doc.RootElement;
            l3Root.TryGetProperty("approach", out var p1).Should().BeTrue("JSON L3 must have approach");
            p1.GetString().Should().NotBeNullOrWhiteSpace();
            l3Root.TryGetProperty("technique", out var p2).Should().BeTrue("JSON L3 must have technique");
            p2.GetString().Should().NotBeNullOrWhiteSpace();
            l3Root.TryGetProperty("key_question", out var p3).Should().BeTrue("JSON L3 must have key_question");
            p3.GetString().Should().NotBeNullOrWhiteSpace();
        }

        ValidateL4Input(l4.Input);

        ValidateExpectedQuality(run, expected);
    }

    public static TokenSavingsMetrics CalculateTokenSavings(BenchmarkRun run)
    {
        string l2Wire = WireOrOutput(SingleLayer(run, "L2_analyst"), "L2");
        string l3Wire = WireOrOutput(SingleLayer(run, "L3_supervisor"), "L3");
        int wireTokens = CountTokenEquivalents(l2Wire + "\n" + l3Wire);

        string l2Compact = ConvertJsonToCompactL2(l2Wire);
        string l3Compact = ConvertJsonToCompactL3(l3Wire);

        bool originalStrictMode = TokenSavingsTracker.StrictCodecG;
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
            TokenSavingsTracker.StrictCodecG = originalStrictMode;
        }
    }

    private static string ConvertJsonToCompactL2(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string em = root.TryGetProperty("emotional_state", out var p1) ? p1.GetString() ?? "none" : "none";
            string sv = root.TryGetProperty("severity", out var p2) ? p2.GetString() ?? "low" : "low";
            string ri = root.TryGetProperty("risk", out var p3) ? p3.GetString() ?? "none" : "none";
            string cp = root.TryGetProperty("patterns", out var p4) ? p4.GetString() ?? "none" : "none";
            string ev = root.TryGetProperty("evidence", out var p5) ? p5.GetString() ?? "none" : "none";
            return $"M|L=2|e7={em}|s9={sv}|x4={ri}|y1={cp}|q3={ev}";
        }
        catch
        {
            return "M|L=2|e7=unknown|s9=low";
        }
    }

    private static string ConvertJsonToCompactL3(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string ap = root.TryGetProperty("approach", out var p1) ? p1.GetString() ?? "behavioral_activation" : "behavioral_activation";
            string tk = root.TryGetProperty("technique", out var p2) ? p2.GetString() ?? "schedule_one_small_activity" : "schedule_one_small_activity";
            string kq = root.TryGetProperty("key_question", out var p3) ? p3.GetString() ?? "What to do?" : "What to do?";
            string rn = root.TryGetProperty("risk_note", out var p4) ? p4.GetString() ?? "none" : "none";
            return $"M|L=3|p3={ap}|t5={tk}|k2={kq}|r8={rn}";
        }
        catch
        {
            return "M|L=3|p3=behavioral_activation";
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

    private static void ValidateL4Input(string input)
    {
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

    private static int CountTokenEquivalents(string text) =>
        (int)Math.Ceiling(text.Length / 3.5);
}
