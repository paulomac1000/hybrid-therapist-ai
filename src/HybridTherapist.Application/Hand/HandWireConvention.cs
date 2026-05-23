using HandCodec.Models;
using RuntimeConvention = HandRuntime.HandWireConvention;

namespace HybridTherapist.Application.Hand;

/// <summary>
/// Application-specific facade. Delegates to <see cref="HandRuntime.HandWireConvention"/>.
/// </summary>
public static class HandWireConvention
{
    /// <inheritdoc cref="HandRuntime.HandWireConvention.PrefillFor(AgentClass)"/>
    public static string PrefillFor(AgentClass agentClass) => RuntimeConvention.PrefillFor(agentClass);

    /// <inheritdoc cref="HandRuntime.HandWireConvention.PrefillFor(Performative, AgentClass)"/>
    public static string PrefillFor(Performative performative, AgentClass agentClass) =>
        RuntimeConvention.PrefillFor(performative, agentClass);

    /// <inheritdoc cref="HandRuntime.HandWireConvention.Example"/>
    public static string Example(double confidence, string answer) =>
        RuntimeConvention.Example(confidence, answer);
}
