using FluentAssertions;
using HandCodec.Models;
using HandCodec.Parser;
using HybridTherapist.Application.Hand;
using Xunit;

namespace HybridTherapist.Tests;

/// <summary>Contract for the System Ping priming checkpoints and the app wire convention.</summary>
public sealed class HandCheckpointLibraryTests
{
    public static TheoryData<HandCheckpoint> AllCheckpoints =>
        new()
        {
            HandCheckpointLibrary.SystemPing,
            HandCheckpointLibrary.MemoPing,
        };

    [Theory]
    [MemberData(nameof(AllCheckpoints))]
    public void Checkpoint_HasExchanges_WithParseableWireLines(HandCheckpoint checkpoint)
    {
        checkpoint.Exchanges.Should().NotBeEmpty();

        foreach (HandExchange ex in checkpoint.Exchanges)
        {
            ex.UserText.Should().NotBeNullOrWhiteSpace();
            ParsedHandMessage? parsed = HandParser.ParseLenient(ex.AssistantWire);
            parsed.Should().NotBeNull("every checkpoint assistant turn must be valid wire format");

            if (parsed!.Performative == Performative.Result)
                parsed.Get("C").Should().NotBeNull("Result checkpoints must carry a numeric confidence");

            if (parsed.Performative == Performative.Memo)
                (parsed.Get("e7") ?? parsed.Get("em")).Should().NotBeNull("Memo checkpoints must carry emotional_state");
        }
    }

    [Fact]
    public void SystemPing_HasBodyWithProtocolAck()
    {
        HandExchange ex = HandCheckpointLibrary.SystemPing.Exchanges[0];
        ParsedHandMessage? parsed = HandParser.ParseLenient(ex.AssistantWire);
        parsed.Should().NotBeNull();
        parsed!.Body.Should().Be("[SYSTEM_PROTOCOL_ACK]",
            "Body (line 2+) must contain a non-therapeutic ack to avoid context pollution");
        parsed.Performative.Should().Be(Performative.Result);
    }

    [Fact]
    public void MemoPing_IsValidMemoWire()
    {
        HandExchange ex = HandCheckpointLibrary.MemoPing.Exchanges[0];
        ParsedHandMessage? parsed = HandParser.ParseLenient(ex.AssistantWire);
        parsed.Should().NotBeNull();
        parsed!.Performative.Should().Be(Performative.Memo);
        parsed.Get("e7").Should().Be("none");
        parsed.Get("s9").Should().Be("low");
        parsed.Get("note").Should().Be("ack");
    }

    [Fact]
    public void SystemPing_ContainsNoTherapeuticWords()
    {
        string allText = string.Join(" ", HandCheckpointLibrary.SystemPing.Exchanges
            .SelectMany(e => new[] { e.UserText, e.AssistantWire }));
        string lower = allText.ToLowerInvariant();

        lower.Should().NotContain("anxiety", "ping must not contain clinical terms");
        lower.Should().NotContain("depression", "ping must not contain clinical terms");
        lower.Should().NotContain("sleep", "ping must not contain clinical terms");
        lower.Should().NotContain("insomnia", "ping must not contain clinical terms");
        lower.Should().NotContain("therapy", "ping must not contain clinical terms");
        lower.Should().NotContain("patient", "ping must not contain clinical terms");
        lower.Should().NotContain("suicide", "ping must not contain clinical terms");
        lower.Should().NotContain("betrayal", "ping must not contain clinical terms");
        lower.Should().NotContain("trauma", "ping must not contain clinical terms");
        lower.Should().NotContain("grief", "ping must not contain clinical terms");
    }

    [Fact]
    public void MemoPing_FieldsAreNonTherapeutic()
    {
        HandExchange ex = HandCheckpointLibrary.MemoPing.Exchanges[0];
        ParsedHandMessage? parsed = HandParser.ParseLenient(ex.AssistantWire);
        parsed.Should().NotBeNull();

        string e7 = (parsed!.Get("e7") ?? parsed.Get("em") ?? "").ToLowerInvariant();
        string s9 = (parsed.Get("s9") ?? parsed.Get("sv") ?? "").ToLowerInvariant();
        string note = (parsed.Get("note") ?? "").ToLowerInvariant();

        e7.Should().Be("none", "emotional_state must be non-therapeutic placeholder");
        s9.Should().Be("low", "severity must be non-therapeutic placeholder");
        note.Should().Be("ack", "note must be protocol acknowledgement");

        string allValues = $"{e7} {s9} {note}";
        allValues.Should().NotContain("anxiety");
        allValues.Should().NotContain("depression");
        allValues.Should().NotContain("sleep");
    }
}
