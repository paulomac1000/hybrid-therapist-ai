using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HybridTherapist.Integration;

/// <summary>
/// End-to-end tests that exercise the FULL Socrates pipeline (L1→L2→L3→L4→L6→L7)
/// against recorded Ollama responses. No live model needed — CI-safe and deterministic.
///
/// Each test points the hybrid-therapist app at a <see cref="CassetteOllamaServer"/> via
/// configuration override, sends a chat request, and asserts the final Polish content
/// emerged from the right layer chain.
/// </summary>
public sealed class CassettePipelineTests
{
    private static string CassettePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "Cassettes", filename);

    [Fact]
    public async Task Insomnia_Scenario_RunsAllSixLayers_ReturnsPolishResponse()
    {
        await using CassetteOllamaServer ollama = await CassetteOllamaServer.StartAsync(CassettePath("socrates-insomnia.json"));

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
            messages = new[] { new { role = "user", content = "nie mogę zasnąć od trzech tygodni" } },
        });

        response.EnsureSuccessStatusCode();
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!;

        // L7 emits the final Polish translation; assert the calibrator's reframing made it through and is in Polish.
        content.Should().Contain("Trzy tygodnie", because: "L7 should have translated the calibrator's reframe");
        content.Should().Contain("?", because: "the therapist should end with an open question");

        // No crisis was detected — metadata reflects normal flow.
        JsonElement meta = doc.RootElement.GetProperty("metadata");
        meta.GetProperty("crisis_detected").GetBoolean().Should().BeFalse();
        meta.GetProperty("fallback").GetBoolean().Should().BeFalse();
        meta.GetProperty("phase").GetString().Should().BeOneOf(["INIT", "EXPLORATION"],
            "phase starts at INIT; medium severity insomnia escalates to EXPLORATION at msg 1 (v0.2.0 phase machine change)");
    }

    [Fact]
    public async Task Gratitude_Scenario_RunsAllSixLayers_ShortPositiveResponse()
    {
        await using CassetteOllamaServer ollama = await CassetteOllamaServer.StartAsync(CassettePath("socrates-gratitude.json"));

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
            messages = new[] { new { role = "user", content = "dziękuję" } },
        });

        response.EnsureSuccessStatusCode();
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!;
        string sessionId = doc.RootElement.GetProperty("metadata").GetProperty("session_id").GetString()!;

        // On failure, dump the trace so we can see which layer broke
        if (!content.Contains("Nie ma za co", StringComparison.Ordinal))
        {
            HttpResponseMessage traceResp = await client.GetAsync($"/v1/trace/{sessionId}");
            string traceBody = await traceResp.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"L7 didn't produce Polish. content=\"{content}\"\nTRACE:\n{traceBody}");
        }

        content.Should().Contain("?", because: "the therapist should end with an open question");
        doc.RootElement.GetProperty("metadata").GetProperty("crisis_detected").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("metadata").GetProperty("fallback").GetBoolean().Should().BeFalse();
    }

    /// <summary>
    /// REGRESSION: reproduces the production "0.95" bug. L7 Bielik copied the
    /// format-hint placeholder literally ("R|V=0.95|C=confidence_decimal\n[real prose]").
    /// The decoder must detect the placeholder remnant in V and surface the prose body.
    /// </summary>
    [Fact]
    public async Task PlaceholderLeak_Scenario_DecoderFallsBackToProseBody()
    {
        await using CassetteOllamaServer ollama = await CassetteOllamaServer.StartAsync(CassettePath("socrates-placeholder-leak.json"));

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
            messages = new[] { new { role = "user", content = "nie mogę zasnąć od trzech tygodni" } },
        });

        response.EnsureSuccessStatusCode();
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!;

        // The bug: response was "0.95". The fix: response should be the actual Polish prose.
        content.Should().NotBe("0.95", because: "the bare confidence number is a placeholder remnant, not the answer");
        content.Should().NotContain("confidence_decimal", because: "the format-hint placeholder must never appear in user-visible output");
        content.Should().Contain("Dziękuję", because: "the real Polish translation from the prose body must win over the bogus V= field");
        content.Length.Should().BeGreaterThan(30, because: "therapeutic prose is multi-sentence");
    }

    [Fact]
    public async Task CassetteServer_ReturnsKnownModels_OnTagsEndpoint()
    {
        await using CassetteOllamaServer ollama = await CassetteOllamaServer.StartAsync(CassettePath("socrates-insomnia.json"));

        using HttpClient probe = new();
        HttpResponseMessage tags = await probe.GetAsync(ollama.BaseUrl + "/api/tags");
        tags.EnsureSuccessStatusCode();

        string body = await tags.Content.ReadAsStringAsync();
        body.Should().Contain("bielik", because: "cassette declares Bielik as an interaction model");
        body.Should().Contain("PsychoCounsel", because: "cassette declares PsychoCounsel as an interaction model");
    }

    /// <summary>
    /// RECOVERY: L2 emits plain prose (no M| wire). Level 4 semantic extraction
    /// must recover the memo fields. Pipeline must not throw — HTTP 200 with valid Polish.
    /// </summary>
    [Fact]
    public async Task Recovery_Scenario_L2EmitsProse_PipelineSurvivesLevel4Recovery()
    {
        await using CassetteOllamaServer ollama = await CassetteOllamaServer.StartAsync(CassettePath("socrates-recovery.json"));

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
            messages = new[] { new { role = "user", content = "nie mogę zasnąć od trzech tygodni" } },
        });

        response.EnsureSuccessStatusCode();
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!;

        content.Should().Contain("Sen", because: "L7 should produce Polish even when L2 failed to emit native M|");
        content.Should().Contain("?", because: "therapist should end with open question");
        content.Length.Should().BeGreaterThan(30, because: "response should be multi-sentence therapeutic prose");

        JsonElement meta = doc.RootElement.GetProperty("metadata");
        meta.GetProperty("fallback").GetBoolean().Should().BeFalse(
            "Level 4 recovery should prevent fallback to static apology");
    }
}
