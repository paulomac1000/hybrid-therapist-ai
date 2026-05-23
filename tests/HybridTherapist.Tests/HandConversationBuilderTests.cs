using FluentAssertions;
using HandCodec.Models;
using HybridTherapist.Application.Hand;
using HybridTherapist.Infrastructure.Adapters;
using Xunit;

namespace HybridTherapist.Tests;

/// <summary>
/// Contract for <see cref="HandConversationBuilder"/> — the format is primed through
/// conversation history (system persona + few-shot exchanges + prefill), never instructed.
/// </summary>
public sealed class HandConversationBuilderTests
{
    private static readonly HandCheckpoint TwoExchange = new(new[]
    {
        new HandExchange("example user one", "R|C=0.9|V=example answer one"),
        new HandExchange("example user two", "R|C=0.8|V=example answer two"),
    });

    private static readonly HandCheckpoint OneMemoExchange = new(new[]
    {
        new HandExchange("[SYSTEM_PROTOCOL_PING]", "M|L=2|em=none|sv=low|note=ack"),
    });

    [Fact]
    public void Build_Assisted_ProducesSystemCheckpointUserPrefillSequence()
    {
        IReadOnlyList<HandTurn> turns = HandConversationBuilder.Build(
            "You are a persona.", TwoExchange, "the real question", AgentClass.Assisted);

        turns.Select(t => t.Role).Should().Equal(
            "system", "user", "assistant", "user", "assistant", "user", "assistant");

        turns[0].Content.Should().Be("You are a persona.");
        turns[1].Content.Should().Be("example user one");
        turns[2].Content.Should().Be("R|C=0.9|V=example answer one");
        turns[5].Content.Should().Be("the real question");
        turns[^1].Content.Should().Be("R|C="); // prefill for Assisted
    }

    [Fact]
    public void Build_Native_HasNoTrailingPrefillTurn()
    {
        IReadOnlyList<HandTurn> turns = HandConversationBuilder.Build(
            "persona", TwoExchange, "question", AgentClass.Native);

        turns.Select(t => t.Role).Should().Equal(
            "system", "user", "assistant", "user", "assistant", "user");
        turns[^1].Should().Be(new HandTurn("user", "question"));
    }

    [Fact]
    public void Build_SystemPromptNeverNamesTheProtocol()
    {
        IReadOnlyList<HandTurn> turns = HandConversationBuilder.Build(
            "You are an empathetic therapist.", TwoExchange, "q", AgentClass.Assisted);

        string system = turns.First(t => t.Role == "system").Content;
        system.Should().NotContain("R|");
        system.Should().NotContain("Start your response");
    }

    [Fact]
    public void Build_NullArguments_Throw()
    {
        Action nullPersona = () => HandConversationBuilder.Build(null!, TwoExchange, "q", AgentClass.Assisted);
        Action nullCheckpoint = () => HandConversationBuilder.Build("p", null!, "q", AgentClass.Assisted);
        Action nullUser = () => HandConversationBuilder.Build("p", TwoExchange, null!, AgentClass.Assisted);

        nullPersona.Should().Throw<ArgumentNullException>();
        nullCheckpoint.Should().Throw<ArgumentNullException>();
        nullUser.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Build_MemoPerformative_UsesMemoPrefill()
    {
        IReadOnlyList<HandTurn> turns = HandConversationBuilder.Build(
            "You are an analyst.", OneMemoExchange, "user input",
            Performative.Memo, AgentClass.Assisted);

        turns[^1].Content.Should().Be("M|L=");
    }

    [Fact]
    public void Build_ResultPerformative_UsesResultPrefill()
    {
        IReadOnlyList<HandTurn> turns = HandConversationBuilder.Build(
            "You are a translator.", HandCheckpointLibrary.SystemPing, "hello",
            Performative.Result, AgentClass.Assisted);

        turns[^1].Content.Should().Be("R|C=");
    }
}
