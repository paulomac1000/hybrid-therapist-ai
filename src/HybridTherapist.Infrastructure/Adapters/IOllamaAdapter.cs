using HandRuntime;

namespace HybridTherapist.Infrastructure.Adapters;

// HandTurn is now provided by HandRuntime — no longer defined here.
// Backward compatibility: consumers that import this namespace still see HandTurn
// through the HandRuntime reference.

public interface IOllamaAdapter
{
    Task<LlmResponse> GenerateAsync(
        string prompt,
        string? systemPrompt,
        int maxTokens,
        float temperature,
        string modelId,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a full multi-turn conversation (used for H.A.N.D. conversation-priming).
    /// A trailing assistant turn acts as a prefill the model continues from.
    /// </summary>
    Task<LlmResponse> GenerateChatAsync(
        IReadOnlyList<HandTurn> messages,
        int maxTokens,
        float temperature,
        string modelId,
        CancellationToken ct = default);
}
