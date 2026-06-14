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
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(userText);
        return RuntimeBuilder.Build(persona, checkpoint, userText, agentClass);
    }

    public static IReadOnlyList<HandTurn> Build(
        string persona,
        HandCheckpoint checkpoint,
        string userText,
        Performative performative,
        AgentClass agentClass)
    {
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(userText);
        return RuntimeBuilder.Build(persona, checkpoint, userText, performative, agentClass);
    }
}
