using FluentAssertions;
using HybridTherapist.Infrastructure.Adapters;
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
}
