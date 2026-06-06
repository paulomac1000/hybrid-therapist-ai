using FluentAssertions;
using System.Text.Json;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace HybridTherapist.Integration;

public sealed class HandCheckpointCountBenchmarkTests
{
    private static string CassettePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "Cassettes", filename);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)] // 1 exchange = 2 messages (1 user ping, 1 assistant ack)
    [InlineData(3, 6)] // 3 exchanges = 6 messages
    [InlineData(5, 6)] // our library only has 3 exchanges max for TherapyAnalystPing, so Take(5) yields 3 exchanges = 6 messages
    public async Task CheckpointCount_ConfiguredValue_AffectsSentMessagesCount(int checkpointCount, int expectedPingMessageCount)
    {
        string cassettePath = CassettePath("hand-anxiety.json");
        await using CassetteOllamaServer ollama = await CassetteOllamaServer.StartAsync(cassettePath);

        await using WebApplicationFactory<Program> app = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Ollama:BaseUrl"] = ollama.BaseUrl,
                        ["Models:ImplicitPrimingCheckpointCount"] = checkpointCount.ToString(),
                        ["Models:HandWireVariant"] = "compact",
                    });
                });
            });

        HttpClient client = app.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/v1/chat/completions", new
        {
            model = "hybrid-therapist",
            messages = new[] { new { role = "user", content = "Od tygodni mam gonitwę myśli i ciągle martwię się o wszystko." } },
        });

        response.EnsureSuccessStatusCode();

        // Inspect requests sent to WireMock for L2 Analyst
#pragma warning disable CS8602 // WireMock Body property lacks NRT annotations
        var chatRequests = ollama.Server.LogEntries
            .Where(le => le.RequestMessage.Path == "/api/chat")
            .Where(le => le.RequestMessage.Body!.Contains("MentaLLaMA"))
            .Select(le => le.RequestMessage.Body!)
            .ToList();
#pragma warning restore CS8602

        chatRequests.Should().NotBeEmpty();
        string requestBody = chatRequests.First();

        using JsonDocument doc = JsonDocument.Parse(requestBody);
        int pingCount = 0;
        if (doc.RootElement.TryGetProperty("messages", out JsonElement msgs) && msgs.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement msg in msgs.EnumerateArray())
            {
                string content = msg.TryGetProperty("content", out JsonElement c) ? c.GetString() ?? "" : "";
                if (content.Contains("[SYSTEM_PROTOCOL_PING]") || content.Contains("M|L=2|e7="))
                {
                    pingCount++;
                }
            }
        }

        pingCount.Should().Be(expectedPingMessageCount, $"configuring {checkpointCount} checkpoints should inject {expectedPingMessageCount} ping messages into conversation history");
    }
}
