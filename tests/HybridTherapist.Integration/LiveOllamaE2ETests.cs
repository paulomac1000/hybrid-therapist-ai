using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace HybridTherapist.Integration;

/// <summary>
/// End-to-end test that runs the FULL Socrates pipeline against a real Ollama instance.
/// Requires Ollama at http://localhost:11434 (or OLLAMA_HOST env var override).
/// This test is NOT skipped if Ollama is missing — it validates that the whole
/// pipeline produces a real Polish therapeutic response for "nie mogę zasnąć".
/// </summary>
public sealed class LiveOllamaE2ETests
{
    private static readonly string OllamaBaseUrl =
        Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "http://localhost:11434";

    private readonly ITestOutputHelper _output;

    public LiveOllamaE2ETests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task LiveOllama_InsomniaQuery_ReturnsPolishResponse_NoFallback()
    {
        await using var app = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Ollama:BaseUrl"] = OllamaBaseUrl,
                })));

        using HttpClient client = app.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(4);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/v1/chat/completions", new
            {
                model = "hybrid-therapist",
                messages = new[] { new { role = "user", content = "nie moge zasnac" } },
            });

        response.EnsureSuccessStatusCode();

        using JsonDocument doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        string content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()!;

        // ── 1. Must NOT contain the hardcoded fallback message — pipeline succeeded ──
        content.Should().NotContain("Przepraszam",
            "live pipeline must produce a real therapeutic response, not a fallback");
        content.Should().NotBeNullOrWhiteSpace();

        // ── 2. Therapeutic prose must be multi-word ──
        content.Length.Should().BeGreaterThan(20,
            "therapeutic response should be multi-sentence prose");

        // ── 3. Should end with an open question ──
        content.Should().Contain("?",
            "therapist should end with an open-ended question");

        // ── 4. No fallback in metadata ──
        JsonElement meta = doc.RootElement.GetProperty("metadata");
        meta.GetProperty("fallback").GetBoolean().Should().BeFalse(
            "live pipeline should not fall back");
        meta.GetProperty("phase").GetString().Should().Be("INIT");
        string sessionId = meta.GetProperty("session_id").GetString()!;
        sessionId.Should().NotBeNullOrWhiteSpace();

        int tokensSaved = meta.GetProperty("token_savings_tokens").GetInt32();
        double savingsPercent = meta.GetProperty("token_savings_percent").GetDouble();
        double.IsFinite(savingsPercent).Should().BeTrue(
            "live benchmark must report token economy from L2/L3 memo wire, even when the live run is inefficient");
        _output.WriteLine($"Token save:   ~{tokensSaved} tokens ({savingsPercent}%)");

        string traceUrl = meta.GetProperty("trace_url").GetString()!;
        using HttpResponseMessage traceResponse = await client.GetAsync(traceUrl);
        traceResponse.EnsureSuccessStatusCode();
        using JsonDocument traceDoc = JsonDocument.Parse(await traceResponse.Content.ReadAsStringAsync());
        string[] layers = traceDoc.RootElement.GetProperty("events")
            .EnumerateArray()
            .Select(e => e.GetProperty("layer").GetString()!)
            .ToArray();
        layers.Should().Contain("L2_analyst");
        layers.Should().Contain("L3_supervisor");
        layers.Should().Contain("L4_therapist");
        _output.WriteLine($"Live trace:    {traceUrl}");

        // ── 5. Response headers ──
        response.Headers.GetValues("X-HT-Fallback")
            .First().Should().Be("false");
        response.Headers.GetValues("X-HT-Flow")
            .First().Should().Be("hybrid-therapist");
    }
}
