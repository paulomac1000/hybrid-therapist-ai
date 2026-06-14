using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HybridTherapist.Application.Hand;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace HybridTherapist.Integration;

internal class HandBenchmarkValidator : HandBenchmarkValidatorBase
{
    private static readonly string[] OldKeys = { "em=", "sv=", "ri=", "cp=", "ap=", "tk=", "kq=", "rn=", "sg=", "cf=" };
    private static readonly string[] VerboseKeys =
    {
        "emotional_state", "severity", "risk_indicators", "cognitive_patterns",
        "approach", "technique", "key_question", "risk_note", "session_goal", "crisis_flag",
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
        ValidateL4CompactInput(l4.Input);
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

    private static void ValidateL4CompactInput(string input)
    {
        input.Should().Contain("M|L=2|", "L4 must receive raw L2 Codec G memo");
        input.Should().Contain("e7=", "L4 raw L2 memo must contain e7=");
        input.Should().Contain("s9=", "L4 raw L2 memo must contain s9=");
        input.Should().Contain("M|L=3|", "L4 must receive raw L3 Codec G memo");
        input.Should().Contain("p3=", "L4 raw L3 memo must contain p3=");
        input.Should().Contain("t5=", "L4 raw L3 memo must contain t5=");
        input.Should().Contain("k2=", "L4 raw L3 memo must contain k2=");

        ValidateL4ForbiddenMarkers(input);
    }

    private static void AssertNoForbiddenKeys(string wire, string label)
    {
        foreach (string oldKey in OldKeys)
            wire.Should().NotContain(oldKey, $"{label} must not contain old key '{oldKey}'");
        foreach (string verboseKey in VerboseKeys)
            wire.Should().NotContain(verboseKey, $"{label} must not contain verbose key '{verboseKey}'");
    }
}
