namespace HybridTherapist.Domain.Models;

public sealed class ChatCompletionRequest
{
    public string Model { get; set; } = string.Empty;
    public List<ChatMessage> Messages { get; set; } = [];
    public bool Stream { get; set; }
}

public sealed class FlowExecutionResult
{
    public string Model { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool Fallback { get; set; }
    public bool CrisisDetected { get; set; }
    public Dictionary<string, object?> Metadata { get; set; } = [];
}

public sealed class LayerResult
{
    public bool Ok { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? ModelId { get; init; }
    public string? Error { get; init; }
    public bool HasCrisisSignal { get; init; }
}
