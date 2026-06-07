using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HybridTherapist.Application.Hand;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using HybridTherapist.Application.Options;

namespace HybridTherapist.Integration;

internal class HandJsonBenchmarkValidator : HandBenchmarkValidatorBase
{
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

        ValidateL4ForbiddenMarkers(l4.Input);

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
}
