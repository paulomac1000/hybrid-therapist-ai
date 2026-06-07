using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HybridTherapist.Application.Hand;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using HybridTherapist.Application.Options;

namespace HybridTherapist.Integration;

internal class HandPlaintextBenchmarkValidator : HandBenchmarkValidatorBase
{
    // Theoretical Compact wire size baseline (~35 tokens) derived from
    // L2 + L3 Codec G memos observed in benchmark cassettes (see docs/benchmarks/hand-compact.md)
    private const int CompactTokensBaseline = 35;

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

        ValidateL4ForbiddenMarkers(l4.Input);

        ValidateExpectedQuality(run, expected);
    }

    public static TokenSavingsMetrics CalculateTokenSavings(BenchmarkRun run)
    {
        string l2Wire = WireOrOutput(SingleLayer(run, "L2_analyst"), "L2");
        string l3Wire = WireOrOutput(SingleLayer(run, "L3_supervisor"), "L3");
        int plaintextTokens = CountTokenEquivalents(l2Wire + "\n" + l3Wire);

        int compactTokens = CompactTokensBaseline;
        double savingsPercent = compactTokens > 0
            ? Math.Round((1.0 - (double)plaintextTokens / compactTokens) * 100, 1)
            : 0;

        return new TokenSavingsMetrics(plaintextTokens, compactTokens, compactTokens - plaintextTokens, savingsPercent);
    }
}
