using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace HybridTherapist.Integration;

/// <summary>
/// Benchmark to check for protocol format drift over long, multi-turn conversations.
/// Simulates a 5-turn session using a WireMock cassette to ensure L2 and L3 layers
/// do not drift out of the strict H.A.N.D. Compact format as the conversation history grows.
/// </summary>
public sealed class HandLongSessionDriftBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public HandLongSessionDriftBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string CassettePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "Cassettes", filename);

    [Fact]
    public async Task LongSession_5Turns_NoProtocolDrift()
    {
        await using CassetteOllamaServer ollama = await CassetteOllamaServer.StartAsync(CassettePath("hand-long-session.json"));

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
        string userSessionId = "drift-test-user";
        string? sessionId = null;

        var turns = new[]
        {
            "Dzień dobry, czuję ostatnio ogromny niepokój.",
            "Ciągle myślę o pracy i nie potrafię się zrelaksować.",
            "Boję się, że zostanę zwolniony, jeśli popełnię błąd.",
            "Codziennie rano budzę się ze ściskiem w żołądku.",
            "Chciałbym nauczyć się jak radzić sobie z tym stresem."
        };

        for (int i = 0; i < turns.Length; i++)
        {
            _output.WriteLine($"[Turn {i + 1}] Sending: {turns[i]}");
            HttpResponseMessage response = await client.PostAsJsonAsync("/v1/chat/completions", new
            {
                model = "hybrid-therapist",
                user = userSessionId,
                messages = new[] { new { role = "user", content = turns[i] } },
            });

            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(responseBody);
            string content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!;
            _output.WriteLine($"[Turn {i + 1}] Received: {content}");

            JsonElement meta = doc.RootElement.GetProperty("metadata");
            meta.GetProperty("fallback").GetBoolean().Should().BeFalse($"Turn {i + 1} must not fall back");

            if (sessionId == null)
            {
                sessionId = meta.GetProperty("session_id").GetString();
                sessionId.Should().NotBeNullOrEmpty();
            }
        }

        sessionId.Should().NotBeNull();

        // Fetch execution trace to analyze L2/L3 outputs
        HttpResponseMessage traceResp = await client.GetAsync($"/v1/trace/{sessionId}");
        traceResp.EnsureSuccessStatusCode();
        using JsonDocument traceDoc = JsonDocument.Parse(await traceResp.Content.ReadAsStringAsync());

        JsonElement events = traceDoc.RootElement.GetProperty("events");
        events.ValueKind.Should().Be(JsonValueKind.Array);

        int l2Count = 0;
        int l3Count = 0;

        foreach (JsonElement ev in events.EnumerateArray())
        {
            string layer = ev.GetProperty("layer").GetString()!;
            string outcome = ev.GetProperty("outcome").GetString()!;
            string? wireFormat = ev.TryGetProperty("wire_format", out var wf) ? wf.GetString() : null;

            if (layer == "L2_analyst")
            {
                l2Count++;
                outcome.Should().Be("ok", "L2 analyst layer outcome must be ok");
                wireFormat.Should().NotBeNullOrWhiteSpace("L2 analyst wire format must be recorded");
                wireFormat.Should().StartWith("M|L=2|", "L2 analyst wire format must follow M|L=2| standard");
                wireFormat.Should().Contain("e7=anxiety", "L2 analyst must correctly analyze anxiety");
                wireFormat.Should().Contain("s9=moderate", "L2 analyst must correctly analyze moderate severity");
                wireFormat.Should().NotContain("decoder_level5_fallback", "L2 analyst must not trigger fallback");
            }
            else if (layer == "L3_supervisor")
            {
                l3Count++;
                outcome.Should().Be("ok", "L3 supervisor layer outcome must be ok");
                wireFormat.Should().NotBeNullOrWhiteSpace("L3 supervisor wire format must be recorded");
                wireFormat.Should().StartWith("M|L=3|", "L3 supervisor wire format must follow M|L=3| standard");
                wireFormat.Should().Contain("p3=cognitive_restructuring", "L3 supervisor must choose cognitive_restructuring");
                wireFormat.Should().Contain("t5=thought_record", "L3 supervisor must select thought_record");
                wireFormat.Should().NotContain("decoder_level5_fallback", "L3 supervisor must not trigger fallback");
            }
        }

        l2Count.Should().Be(5, "Should have exactly 5 L2 Analyst trace entries");
        l3Count.Should().Be(5, "Should have exactly 5 L3 Supervisor trace entries");

        _output.WriteLine("Long-session drift benchmark successfully completed with 0 drift detected!");
    }
}
