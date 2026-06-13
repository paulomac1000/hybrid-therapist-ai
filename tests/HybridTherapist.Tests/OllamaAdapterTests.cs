using FluentAssertions;
using HybridTherapist.Infrastructure.Adapters;
using HybridTherapist.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HybridTherapist.Tests;

public sealed class OllamaAdapterTests
{
    [Fact]
    public async Task GenerateChatAsync_UnreachableHost_ReturnsErrorResponse()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("ollama", c =>
        {
            c.BaseAddress = new Uri("http://127.0.0.1:19999");
            c.Timeout = TimeSpan.FromSeconds(2);
        });
        ServiceProvider sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        var adapter = new OllamaAdapter(factory);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        LlmResponse result = await adapter.GenerateChatAsync(
            [new HandTurn("user", "test")], 100, 0.1f, "test-model", cts.Token);

        result.Ok.Should().BeFalse("unreachable host must produce an error");
        result.Error.Should().NotBeNullOrEmpty("error message must be present");
    }

    [Fact]
    public async Task GenerateChatAsync_Http500Error_ReturnsErrorResponse()
    {
        var fake = new FakeOllamaAdapter(
            new LlmResponse { Ok = false, Error = "Ollama 500: internal error", ModelId = "test" });

        LlmResponse result = await fake.GenerateChatAsync(
            [new HandTurn("user", "test")], 100, 0.1f, "test-model");

        result.Ok.Should().BeFalse("HTTP 500 must produce an error");
        result.Error.Should().NotBeNullOrEmpty();
        result.Error.Should().Contain("500", "error message should reference the HTTP status code");
    }

    [Fact]
    public async Task GenerateChatAsync_MalformedJsonResponse_ReturnsErrorResponse()
    {
        var fake = new FakeOllamaAdapter(
            new LlmResponse { Ok = false, Error = "JSON parse error: unexpected token at position 42", ModelId = "test" });

        LlmResponse result = await fake.GenerateChatAsync(
            [new HandTurn("user", "test")], 100, 0.1f, "test-model");

        result.Ok.Should().BeFalse("malformed JSON must produce an error");
        result.Error.Should().NotBeNullOrEmpty();
        result.Error.Should().Contain("JSON", "error message should reference JSON parsing failure");
    }

    [Fact]
    public async Task GenerateChatAsync_EmptyResponse_ReturnsSuccessWithEmptyText()
    {
        var fake = new FakeOllamaAdapter(
            new LlmResponse { Ok = true, Text = string.Empty, ModelId = "test" });

        LlmResponse result = await fake.GenerateChatAsync(
            [new HandTurn("user", "test")], 100, 0.1f, "test-model");

        result.Ok.Should().BeTrue("empty response is still a successful HTTP round-trip");
        result.Text.Should().BeEmpty("the model returned no content");
        result.Error.Should().BeNull("no error occurred");
    }

    [Fact]
    public async Task GenerateChatAsync_Unauthorized_ReturnsErrorResponse()
    {
        var fake = new FakeOllamaAdapter(
            new LlmResponse { Ok = false, Error = "Ollama 401: invalid API key", ModelId = "test" });

        LlmResponse result = await fake.GenerateChatAsync(
            [new HandTurn("user", "test")], 100, 0.1f, "test-model");

        result.Ok.Should().BeFalse("401 Unauthorized must produce an error");
        result.Error.Should().NotBeNullOrEmpty();
        result.Error.Should().Contain("401", "error message should reference the HTTP status code");
    }

    [Fact]
    public async Task GenerateChatAsync_Timeout_ReturnsErrorResponse()
    {
        var fake = new FakeOllamaAdapter(
            new LlmResponse { Ok = false, Error = "The operation was cancelled due to timeout", ModelId = "test" });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

        LlmResponse result = await fake.GenerateChatAsync(
            [new HandTurn("user", "test")], 100, 0.1f, "test-model", cts.Token);

        result.Ok.Should().BeFalse("timeout must produce an error");
        result.Error.Should().NotBeNullOrEmpty();
        result.Error.Should().Contain("timeout", "error message should reference timeout");
    }

    [Fact]
    public async Task GenerateAsync_Error_ReturnsErrorResponse()
    {
        var fake = new FakeOllamaAdapter(
            new LlmResponse { Ok = false, Error = "model not found", ModelId = "test" });

        LlmResponse result = await fake.GenerateAsync(
            "prompt", null, 100, 0.1f, "test-model");

        result.Ok.Should().BeFalse("GenerateAsync must propagate errors");
        result.Error.Should().Be("model not found");
    }

    [Fact]
    public async Task GenerateChatAsync_And_GenerateAsync_ShareErrorContract()
    {
        var fake = new FakeOllamaAdapter(
            new LlmResponse { Ok = false, Error = "shared error contract", ModelId = "test" });

        LlmResponse chatResult = await fake.GenerateChatAsync(
            [new HandTurn("user", "hello")], 100, 0.1f, "test-model");
        LlmResponse genResult = await fake.GenerateAsync(
            "hello", null, 100, 0.1f, "test-model");

        chatResult.Ok.Should().BeFalse();
        genResult.Ok.Should().BeFalse();
        chatResult.Error.Should().Be(genResult.Error,
            "both methods must report errors through the same LlmResponse contract");
    }
}
