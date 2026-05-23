using HandCodec.Models;
using RuntimeBuilder = HandRuntime.HandConversationBuilder;

namespace HybridTherapist.Application.Hand;

/// <summary>
/// Application-specific facade. Delegates to <see cref="HandRuntime.HandConversationBuilder"/>.
/// </summary>
public static class HandConversationBuilder
{
    public static IReadOnlyList<HandTurn> Build(
        string persona,
        HandCheckpoint checkpoint,
        string userText,
        AgentClass agentClass)
    {
        return RuntimeBuilder.Build(persona, checkpoint, userText, agentClass);
    }

    public static IReadOnlyList<HandTurn> Build(
        string persona,
        HandCheckpoint checkpoint,
        string userText,
        Performative performative,
        AgentClass agentClass)
    {
        return RuntimeBuilder.Build(persona, checkpoint, userText, performative, agentClass);
    }
}
