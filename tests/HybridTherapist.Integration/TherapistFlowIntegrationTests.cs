using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HybridTherapist.Integration;

/// <summary>
/// Integration tests for /v1/chat/completions against a real in-process server.
/// These tests do NOT require Ollama — they verify the HTTP contract
/// and the pipeline structure (crisis gate, input validation, response shape).
/// Ollama URL is forced to a dead endpoint so the pipeline always exercises
/// the fast-fallback path regardless of whether a real Ollama is running.
/// </summary>
public sealed class TherapistFlowIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TherapistFlowIntegrationTests(WebApplicationFactory<Program> factory)
    {
        WebApplicationFactory<Program> modified = factory.WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Ollama:BaseUrl"] = "http://127.0.0.1:19999",
                })));
        _client = modified.CreateClient();
    }

    // ── /v1/models ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetModels_Returns200_WithHybridTherapist()
    {
        HttpResponseMessage response = await _client.GetAsync("/v1/models");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var ids = doc.RootElement.GetProperty("data").EnumerateArray()
            .Select(e => e.GetProperty("id").GetString())
            .ToList();

        ids.Should().Contain("hybrid-therapist");
    }

    // ── Input validation ──────────────────────────────────────────────────────

    [Fact]
    public async Task PostCompletions_EmptyModel_Returns400()
    {
        var payload = new { model = "", messages = new[] { new { role = "user", content = "hello" } } };
        HttpResponseMessage response = await _client.PostAsJsonAsync("/v1/chat/completions", payload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostCompletions_EmptyMessages_Returns400()
    {
        var payload = new { model = "hybrid-therapist", messages = Array.Empty<object>() };
        HttpResponseMessage response = await _client.PostAsJsonAsync("/v1/chat/completions", payload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostCompletions_MissingModel_Returns400()
    {
        var payload = new { messages = new[] { new { role = "user", content = "test" } } };
        HttpResponseMessage response = await _client.PostAsJsonAsync("/v1/chat/completions", payload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Crisis gate contract (no LLM needed) ─────────────────────────────────

    [Fact]
    public async Task PostCompletions_CrisisInput_Returns200_WithHelplineNumber()
    {
        var payload = new
        {
            model = "hybrid-therapist",
            messages = new[] { new { role = "user", content = "chcę się zabić" } },
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync("/v1/chat/completions", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        string content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        content.Should().Contain("116 123", "crisis response must include Polish helpline");
    }

    [Fact]
    public async Task PostCompletions_CrisisInput_ResponseHasAssistantRole()
    {
        var payload = new
        {
            model = "hybrid-therapist",
            messages = new[] { new { role = "user", content = "myślę o samobójstwie" } },
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync("/v1/chat/completions", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        string role = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("role")
            .GetString() ?? string.Empty;

        role.Should().Be("assistant", "OpenAI contract: role must always be 'assistant'");
    }

    // ── Response contract ─────────────────────────────────────────────────────

    [Fact]
    public async Task PostCompletions_ValidRequest_HasRequiredResponseHeaders()
    {
        var payload = new
        {
            model = "hybrid-therapist",
            messages = new[] { new { role = "user", content = "nie mogę zasnąć" } },
        };

        // This will fail at L1 (no Ollama) and return fallback — but headers must still be set
        HttpResponseMessage response = await _client.PostAsJsonAsync("/v1/chat/completions", payload);

        response.Headers.TryGetValues("X-HT-Flow", out var flowValues).Should().BeTrue();
        flowValues!.First().Should().Be("hybrid-therapist");
        response.Headers.Contains("X-HT-Fallback").Should().BeTrue();
    }

    [Fact]
    public async Task PostCompletions_ValidRequest_ResponseBodyIsOpenAiShape()
    {
        var payload = new
        {
            model = "hybrid-therapist",
            messages = new[] { new { role = "user", content = "test" } },
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync("/v1/chat/completions", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        root.TryGetProperty("id", out _).Should().BeTrue("response must have 'id'");
        root.TryGetProperty("choices", out var choices).Should().BeTrue("response must have 'choices'");
        choices.GetArrayLength().Should().BeGreaterThan(0, "choices must not be empty");

        var message = choices[0].GetProperty("message");
        message.TryGetProperty("role", out var roleEl).Should().BeTrue("message must have 'role'");
        roleEl.GetString().Should().Be("assistant");
        message.TryGetProperty("content", out _).Should().BeTrue("message must have 'content'");
    }

    // ── SSE streaming contract (LibreChat sends stream:true by default) ─────────

    [Fact]
    public async Task PostCompletions_StreamTrue_ReturnsSse_WithDeltaRoleAndContent()
    {
        var payload = new
        {
            model = "hybrid-therapist",
            messages = new[] { new { role = "user", content = "chcę skończyć z sobą" } },
            stream = true,
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync("/v1/chat/completions", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");

        string body = await response.Content.ReadAsStringAsync();
        body.Should().StartWith("data: ", "SSE chunks must use 'data: ' prefix");
        body.Should().EndWith("data: [DONE]\n\n", "OpenAI SSE stream terminates with [DONE]");

        // Parse first chunk and verify LibreChat-required shape
        string firstChunkJson = body.Split("\n\n")[0][6..]; // strip "data: "
        using JsonDocument first = JsonDocument.Parse(firstChunkJson);
        JsonElement firstChoice = first.RootElement.GetProperty("choices")[0];
        firstChoice.GetProperty("delta").GetProperty("role").GetString()
            .Should().Be("assistant", "LibreChat reads delta.role from the first chunk");
        firstChoice.GetProperty("delta").GetProperty("content").GetString()
            .Should().Contain("116 123", "crisis response content must travel through SSE delta");

        // Parse last data chunk (before [DONE]) — must have finish_reason
        string[] chunks = body.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        string finalDataChunk = chunks[^2][6..]; // last data: chunk before [DONE]
        using JsonDocument last = JsonDocument.Parse(finalDataChunk);
        last.RootElement.GetProperty("choices")[0].GetProperty("finish_reason").GetString()
            .Should().Be("stop", "final SSE chunk must signal completion");
    }

    [Fact]
    public async Task PostCompletions_StreamTrue_HasHTHeaders()
    {
        var payload = new
        {
            model = "hybrid-therapist",
            messages = new[] { new { role = "user", content = "chcę skończyć z sobą" } },
            stream = true,
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync("/v1/chat/completions", payload);
        response.Headers.GetValues("X-HT-Flow").First().Should().Be("hybrid-therapist");
        response.Headers.Contains("X-HT-Fallback").Should().BeTrue();
    }
}
