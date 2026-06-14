using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HybridTherapist.Integration;

public sealed class ChatEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ChatEndpointsTests(WebApplicationFactory<Program> factory)
    {
        WebApplicationFactory<Program> modified = factory.WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Ollama:BaseUrl"] = "http://localhost:19999",
                })));
        _client = modified.CreateClient();
    }

    [Fact]
    public async Task PostChatCompletions_IncompleteJson_Returns400()
    {
        var content = new StringContent("{invalid}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _client.PostAsync("/v1/chat/completions", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Contain("Invalid JSON");
    }

    [Fact]
    public async Task PostChatCompletions_EmptyBody_Returns400()
    {
        var content = new StringContent("", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _client.PostAsync("/v1/chat/completions", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostChatCompletions_MissingModel_Returns400()
    {
        var body = new { messages = new[] { new { role = "user", content = "hello" } } };

        HttpResponseMessage response = await _client.PostAsJsonAsync("/v1/chat/completions", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostChatCompletions_ValidRequest_IsRouted()
    {
        var body = new
        {
            model = "hybrid-therapist",
            messages = new[] { new { role = "user", content = "hello" } },
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync("/v1/chat/completions", body);

        // Smoke test: pipeline may gracefully degrade (503, 504, or 200),
        // but confirmed the route exists and request reached the handler
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetModels_Returns200()
    {
        HttpResponseMessage response = await _client.GetAsync("/v1/models");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

public sealed class TraceEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TraceEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetTrace_UnknownSession_ReturnsEmptyEventList()
    {
        HttpResponseMessage response = await _client.GetAsync("/v1/trace/nonexistent-session-id");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("event_count").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task GetTrace_TraceEndpoint_ReturnsJson()
    {
        HttpResponseMessage response = await _client.GetAsync("/v1/trace/test-session-123");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("session_id", out _).Should().BeTrue();
    }
}
