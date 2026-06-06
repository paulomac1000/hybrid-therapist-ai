using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HybridTherapist.Application.Hand;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using HybridTherapist.Application.Options;

namespace HybridTherapist.Integration;

internal static class HandPlaintextBenchmarkValidator
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
                        ["Models:HandWireVariant"] = "Plaintext",
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

    public static void ValidatePlaintextStrict(BenchmarkRun run, BenchmarkExpectations expected)
    {
        run.Metadata.Fallback.Should().BeFalse("strict plaintext benchmark must not pass through fallback");

        BenchmarkTraceEvent l2 = SingleLayer(run, "L2_analyst");
        BenchmarkTraceEvent l3 = SingleLayer(run, "L3_supervisor");
        BenchmarkTraceEvent l4 = SingleLayer(run, "L4_therapist");

        l2.Outcome.Should().Be("ok", "L2 must successfully emit Plaintext, not error fallback");
        l3.Outcome.Should().Be("ok", "L3 must successfully emit Plaintext, not error fallback");

        string l2Wire = WireOrOutput(l2, "L2");
        string l3Wire = WireOrOutput(l3, "L3");

        l2Wire.Should().NotStartWith("M|", "plaintext L2 must not emit wire format");
        l2Wire.Should().Contain("Emotional state:", "plaintext L2 must label emotional state");
        l2Wire.Should().Contain("Severity:", "plaintext L2 must label severity");

        l3Wire.Should().NotStartWith("M|", "plaintext L3 must not emit wire format");
        l3Wire.Should().Contain("Approach:", "plaintext L3 must label approach");
        l3Wire.Should().Contain("Technique:", "plaintext L3 must label technique");
        l3Wire.Should().Contain("Key question:", "plaintext L3 must label key question");

        foreach (string marker in L4ForbiddenInstructionMarkers)
            l4.Input.Should().NotContain(marker, $"L4 prompt must not contain format instruction marker '{marker}'");

        ValidateExpectedQuality(run, expected);
    }

    public static TokenSavingsMetrics CalculateTokenSavings(BenchmarkRun run)
    {
        string l2Wire = WireOrOutput(SingleLayer(run, "L2_analyst"), "L2");
        string l3Wire = WireOrOutput(SingleLayer(run, "L3_supervisor"), "L3");
        int plaintextTokens = CountTokenEquivalents(l2Wire + "\n" + l3Wire);

        // Theoretical Compact wire size baseline is ~35 tokens
        int compactTokens = 35;
        double savingsPercent = compactTokens > 0
            ? Math.Round((1.0 - (double)plaintextTokens / compactTokens) * 100, 1)
            : 0;

        return new TokenSavingsMetrics(plaintextTokens, compactTokens, compactTokens - plaintextTokens, savingsPercent);
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
