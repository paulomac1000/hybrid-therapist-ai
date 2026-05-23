using HybridTherapist.Infrastructure.Adapters;

namespace HybridTherapist.Tests.Fakes;

public sealed class FakeOllamaAdapter : IOllamaAdapter
{
    private readonly LlmResponse _defaultResponse;
    private readonly Dictionary<string, LlmResponse>? _perModel;

    public FakeOllamaAdapter(LlmResponse response)
    {
        _defaultResponse = response;
    }

    public FakeOllamaAdapter(string text)
        : this(new LlmResponse { Ok = true, Text = text, ModelId = "fake" })
    {
    }

    public FakeOllamaAdapter(Dictionary<string, LlmResponse> perModel)
    {
        _perModel = perModel;
        _defaultResponse = new LlmResponse { Ok = true, Text = string.Empty, ModelId = "fake" };
    }

    public Task<LlmResponse> GenerateAsync(
        string prompt,
        string? systemPrompt,
        int maxTokens,
        float temperature,
        string modelId,
        CancellationToken ct = default)
    {
        return Task.FromResult(Resolve(modelId));
    }

    public Task<LlmResponse> GenerateChatAsync(
        IReadOnlyList<HandTurn> messages,
        int maxTokens,
        float temperature,
        string modelId,
        CancellationToken ct = default)
    {
        return Task.FromResult(Resolve(modelId));
    }

    private LlmResponse Resolve(string modelId)
    {
        if (_perModel is not null && _perModel.TryGetValue(modelId, out LlmResponse? matched))
            return matched;
        return _defaultResponse;
    }
}
