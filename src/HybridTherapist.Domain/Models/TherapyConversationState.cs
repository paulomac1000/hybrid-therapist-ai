namespace HybridTherapist.Domain.Models;

public sealed class TherapyConversationState
{
    public string SessionId { get; set; } = string.Empty;
    public string CurrentPhase { get; set; } = "INIT";
    public int MessageCount { get; set; }
    public int MessagesInPhase { get; set; }
    public List<string> Topics { get; set; } = [];
    public List<ChatMessage> History { get; set; } = [];
    public string? SessionSummary { get; set; }
    public MemorySummary? StructuredSummary { get; set; }
}

public sealed class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
