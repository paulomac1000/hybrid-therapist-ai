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

        // ── 5. No model thinking/control tokens leaked ──
        content.Should().NotContain("<|control",
            "model thinking tokens (PsychoCounsel <|control_N|>) must be stripped before output");

        // ── 6. Response must be Polish (has diacritics) ──
        content.Should().ContainAny(new[] { "ą", "ć", "ę", "ł", "ń", "ó", "ś", "ź", "ż" },
            "therapeutic response must be in Polish with proper diacritics");

        // ── 7. Analyst severity — logged (may be "unknown" in WebApplicationFactory context)
        _output.WriteLine($"Severity: {meta.GetProperty("analyst_severity").GetString()}");

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

    [Fact]
    public async Task LiveOllama_CrisisInput_ReturnsHelpline_NoLlmCall()
    {
        await using var app = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Ollama:BaseUrl"] = OllamaBaseUrl,
                })));

        using HttpClient client = app.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/v1/chat/completions", new
            {
                model = "hybrid-therapist",
                messages = new[] { new { role = "user", content = "chcę skończyć z sobą" } },
            });

        response.EnsureSuccessStatusCode();

        using JsonDocument doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        string content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()!;

        // CrisisGate must return Polish helpline number
        content.Should().Contain("116 123",
            "crisis input must trigger hard-stop with helpline number");
        content.Should().Contain("Telefon Zaufania",
            "crisis response must be in Polish and mention the helpline");

        JsonElement meta = doc.RootElement.GetProperty("metadata");
        meta.GetProperty("crisis_detected").GetBoolean().Should().BeTrue(
            "crisis input must set crisis_detected flag");

        // CrisisGate stops at layer -1 — no LLM calls, so no fallback path
        meta.GetProperty("fallback").GetBoolean().Should().BeFalse();
        meta.GetProperty("crisis_severity").GetString().Should().Be("critical");

        _output.WriteLine($"Crisis hard-stop response: {content}");
    }

    [Fact]
    public async Task LiveOllama_GreetingQuery_ReturnsPolishResponse()
    {
        (string content, JsonElement meta, JsonDocument doc) = await ExecuteTherapyQuery("witaj");

        content.Should().NotContain("Przepraszam");
        content.Should().NotContain("<|control");
        content.Should().ContainAny("ą", "ć", "ę", "ł", "ń", "ó", "ś", "ź", "ż");
        meta.GetProperty("fallback").GetBoolean().Should().BeFalse();
        _output.WriteLine($"Severity: {meta.GetProperty("analyst_severity").GetString()}");
        meta.GetProperty("phase").GetString().Should().Be("INIT");
    }

    [Fact]
    public async Task LiveOllama_DepressionQuery_NoFabricatedThemes()
    {
        (string content, JsonElement meta, JsonDocument doc) = await ExecuteTherapyQuery("czuję się smutny od tygodnia");

        content.Should().NotContain("Przepraszam");
        content.Should().NotContain("<|control");
        content.Should().ContainAny("ą", "ć", "ę", "ł", "ń", "ó", "ś", "ź", "ż");
        meta.GetProperty("fallback").GetBoolean().Should().BeFalse();

        // Check the trace — analyst should NOT fabricate themes for this input
        string traceUrl = meta.GetProperty("trace_url").GetString()!;
        await AssertNoFabricatedThemes(doc, traceUrl, new[] { "racing_thoughts", "panic" });
    }

    [Fact]
    public async Task LiveOllama_AnxietyQuery_NoFabricatedThemes()
    {
        (string content, JsonElement meta, JsonDocument doc) = await ExecuteTherapyQuery("ciągle się martwię o pracę");

        content.Should().NotContain("Przepraszam");
        content.Should().NotContain("<|control");
        content.Should().ContainAny("ą", "ć", "ę", "ł", "ń", "ó", "ś", "ź", "ż");
        meta.GetProperty("fallback").GetBoolean().Should().BeFalse();
        _output.WriteLine($"Severity: {meta.GetProperty("analyst_severity").GetString()}");

        string traceUrl = meta.GetProperty("trace_url").GetString()!;
        await AssertNoFabricatedThemes(doc, traceUrl, new[] { "panic", "hopelessness" });
    }

    [Fact]
    public async Task LiveOllama_PositiveFeedback_ReturnsResponse()
    {
        (string content, JsonElement meta, JsonDocument doc) = await ExecuteTherapyQuery("dziękuję, pomogło mi to");

        content.Should().NotContain("Przepraszam");
        content.Should().NotContain("<|control");
        content.Should().ContainAny("ą", "ć", "ę", "ł", "ń", "ó", "ś", "ź", "ż");
        meta.GetProperty("fallback").GetBoolean().Should().BeFalse();
        meta.GetProperty("phase").GetString().Should().Be("INIT");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Common setup: create app, send query, return response parts.</summary>
    private async Task<(string content, JsonElement metadata, JsonDocument doc)> ExecuteTherapyQuery(string userInput)
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
                messages = new[] { new { role = "user", content = userInput } },
            });

        response.EnsureSuccessStatusCode();

        JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string content = doc.RootElement.GetProperty("choices")[0]
            .GetProperty("message").GetProperty("content").GetString()!;
        JsonElement meta = doc.RootElement.GetProperty("metadata");

        _output.WriteLine($"Input: {userInput}");
        _output.WriteLine($"Severity: {meta.GetProperty("analyst_severity").GetString()}");

        return (content, meta, doc);
    }

    /// <summary>Fetches the L2 trace and asserts no fabricated themes are present.</summary>
    private async Task AssertNoFabricatedThemes(JsonDocument responseDoc, string traceUrl, string[] forbiddenL2Terms)
    {
        // Fetch the trace from the in-process server (URL is relative)
        await using var traceApp = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Ollama:BaseUrl"] = OllamaBaseUrl,
                })));

        using HttpClient client = traceApp.CreateClient();
        using HttpResponseMessage traceResponse = await client.GetAsync(traceUrl);
        traceResponse.EnsureSuccessStatusCode();

        using JsonDocument traceDoc = JsonDocument.Parse(await traceResponse.Content.ReadAsStringAsync());
        foreach (JsonElement evt in traceDoc.RootElement.GetProperty("events").EnumerateArray())
        {
            string layer = evt.GetProperty("layer").GetString()!;
            if (layer != "L2_analyst") continue;

            string output = (evt.GetProperty("output").GetString() ?? "").ToLowerInvariant();
            string wireFormat = (evt.GetProperty("wire_format").GetString() ?? output).ToLowerInvariant();

            foreach (string term in forbiddenL2Terms)
            {
                wireFormat.Should().NotContain(term.ToLowerInvariant(),
                    $"L2 analyst must not fabricate '{term}' — user did not mention it");
            }
            _output.WriteLine($"L2 trace OK — no fabricated terms found");
        }
    }

    // ── Multi-message conversation tests ───────────────────────────────────

    private async Task<(string content, JsonElement metadata, JsonDocument doc)> ExecuteMultiMessage(params string[] userMessages)
    {
        await using var app = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Ollama:BaseUrl"] = OllamaBaseUrl,
                })));

        using HttpClient client = app.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(4);

        var history = new List<object>();
        JsonDocument lastDoc = null!;

        for (int i = 0; i < userMessages.Length; i++)
        {
            var msgList = history.Concat(new[]
            {
                new { role = "user", content = userMessages[i] }
            }).ToArray();

            using HttpResponseMessage response = await client.PostAsJsonAsync(
                "/v1/chat/completions", new
                {
                    model = "hybrid-therapist",
                    messages = msgList,
                });

            response.EnsureSuccessStatusCode();
            lastDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            string reply = lastDoc.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString()!;

            string truncated = userMessages[i][..Math.Min(40, userMessages[i].Length)];
            _output.WriteLine($"msg{i + 1}: {truncated}... → len={reply.Length}");

            history.Add(new { role = "user", content = userMessages[i] });
            history.Add(new { role = "assistant", content = reply });
        }

        string lastContent = lastDoc.RootElement.GetProperty("choices")[0]
            .GetProperty("message").GetProperty("content").GetString()!;
        JsonElement lastMeta = lastDoc.RootElement.GetProperty("metadata");

        return (lastContent, lastMeta, lastDoc);
    }

    [Fact]
    public async Task LiveOllama_MultiTurn_PhaseTransition()
    {
        (string content, JsonElement meta, JsonDocument doc) = await ExecuteMultiMessage(
            "nie mogę zasnąć",
            "budzę się o 3 w nocy",
            "to trwa już miesiąc");

        content.Should().NotContain("Przepraszam");
        content.Should().NotContain("<|control");
        content.Should().ContainAny("ą", "ć", "ę", "ł", "ń", "ó", "ś", "ź", "ż");
        meta.GetProperty("fallback").GetBoolean().Should().BeFalse();
        meta.GetProperty("message_count").GetInt32().Should().BeGreaterThanOrEqualTo(3);
        _output.WriteLine($"Severity: {meta.GetProperty("analyst_severity").GetString()}");
    }

    [Fact]
    public async Task LiveOllama_MultiTurn_RuptureDetection()
    {
        (string content, JsonElement meta, JsonDocument doc) = await ExecuteMultiMessage(
            "nie mogę zasnąć od miesiąca",
            "to mi nie pomaga, potrzebuję konkretnych technik");

        content.Should().NotContain("Przepraszam");
        content.Should().NotContain("<|control");
        meta.GetProperty("fallback").GetBoolean().Should().BeFalse();
        meta.GetProperty("rupture_detected").GetBoolean().Should().BeTrue(
            "user frustration must trigger rupture detection");
        meta.GetProperty("strategy").GetString().Should().Be("Repair",
            "rupture must force Repair strategy");
        meta.GetProperty("rupture_reason").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LiveOllama_MultiTurn_ConcreteTechniqueRequest()
    {
        (string content, JsonElement meta, JsonDocument doc) = await ExecuteMultiMessage(
            "ciągle się martwię",
            "co konkretnie mam zrobić?",
            "próbowałem to nie działa");

        content.Should().NotContain("Przepraszam");
        content.Should().NotContain("<|control");
        meta.GetProperty("fallback").GetBoolean().Should().BeFalse();
        meta.GetProperty("phase").GetString().Should().NotBe("INIT");
        content.Should().Contain("?", "therapeutic response should end with a question");
    }

    [Fact]
    public async Task LiveOllama_MultiTurn_MemoryContext()
    {
        (string content, JsonElement meta, JsonDocument doc) = await ExecuteMultiMessage(
            "mam problemy w pracy",
            "szef ciągle na mnie krzyczy",
            "przez to nie mogę spać",
            "czy powinienem zmienić pracę?");

        content.Should().NotContain("<|control");
        bool fb = meta.GetProperty("fallback").GetBoolean();
        _output.WriteLine($"Fallback: {fb}");
        _output.WriteLine($"Msg count: {(meta.TryGetProperty("message_count", out var mc) ? mc.GetInt32().ToString() : "N/A")}");
        _output.WriteLine($"Response (first 150): {content[..Math.Min(150, content.Length)]}");
        if (!fb)
        {
            JsonElement topics = meta.GetProperty("topics");
            topics.GetArrayLength().Should().BeGreaterThan(0);
        }

        // Check trace for L2 memo containing accumulated themes
        string traceUrl = meta.GetProperty("trace_url").GetString()!;
        await using var traceApp = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Ollama:BaseUrl"] = OllamaBaseUrl,
                })));
        using HttpClient traceClient = traceApp.CreateClient();
        using HttpResponseMessage traceResponse = await traceClient.GetAsync(traceUrl);
        traceResponse.EnsureSuccessStatusCode();
        using JsonDocument traceDoc = JsonDocument.Parse(await traceResponse.Content.ReadAsStringAsync());
        string[] eventLayers = traceDoc.RootElement.GetProperty("events")
            .EnumerateArray().Select(e => e.GetProperty("layer").GetString()!).ToArray();
        eventLayers.Should().Contain("L5_memory");
        _output.WriteLine("Memory context verified — L5 memory layer present in trace");
    }
}
