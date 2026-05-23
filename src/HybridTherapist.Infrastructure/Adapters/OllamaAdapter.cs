using System.Net.Http.Json;
using System.Text.Json;
using HandRuntime;

namespace HybridTherapist.Infrastructure.Adapters;

public sealed class LlmResponse
{
    public bool Ok { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? Error { get; init; }
    public string? ModelId { get; init; }
}

/// <summary>
/// HTTP adapter for Ollama's /api/chat endpoint with per-layer timeout (default 180s).
/// Uses IHttpClientFactory — never instantiates HttpClient directly.
/// Each call creates a linked CancellationTokenSource that auto-cancels after the timeout
/// even if the client's overall timeout has not elapsed.
/// </summary>
public sealed class OllamaAdapter : IOllamaAdapter
{
    private readonly IHttpClientFactory _factory;

    public OllamaAdapter(IHttpClientFactory factory) => _factory = factory;

    public Task<LlmResponse> GenerateAsync(
        string prompt, string? systemPrompt, int maxTokens, float temperature,
        string modelId, CancellationToken ct = default)
    {
        object[] messages = string.IsNullOrWhiteSpace(systemPrompt)
            ? [new { role = "user", content = prompt }]
            : [new { role = "system", content = systemPrompt }, new { role = "user", content = prompt }];

        return SendAsync(messages, maxTokens, temperature, modelId, ct);
    }

    public Task<LlmResponse> GenerateChatAsync(
        IReadOnlyList<HandTurn> messages, int maxTokens, float temperature,
        string modelId, CancellationToken ct = default)
    {
        object[] mapped = messages
            .Select(m => (object)new { role = m.Role, content = m.Content })
            .ToArray();

        return SendAsync(mapped, maxTokens, temperature, modelId, ct);
    }

    private async Task<LlmResponse> SendAsync(
        object[] messages, int maxTokens, float temperature, string modelId, CancellationToken ct,
        int timeoutSeconds = 180)
    {
        var client = _factory.CreateClient("ollama");

        var body = new
        {
            model = modelId,
            messages,
            stream = false,
            options = new { num_predict = maxTokens, temperature },
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        CancellationToken linked = timeoutCts.Token;

        try
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync("/api/chat", body, linked);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync(linked);
                return new LlmResponse
                {
                    Ok = false,
                    Error = $"Ollama {response.StatusCode}: {error[..Math.Min(200, error.Length)]}",
                };
            }

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(linked), cancellationToken: linked);

            string text = doc.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            return new LlmResponse { Ok = true, Text = text.Trim(), ModelId = modelId };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new LlmResponse { Ok = false, Error = ex.Message };
        }
    }
}
