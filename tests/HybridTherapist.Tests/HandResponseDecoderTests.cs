using FluentAssertions;
using HandCodec.Models;
using HybridTherapist.Application.Hand;
using Xunit;

namespace HybridTherapist.Tests;

/// <summary>
/// Behavioural contract for <see cref="HandResponseDecoder"/> — the codec-correct
/// replacement for the old TherapistHandDecoder. Wire convention: R|C=&lt;conf&gt;|V=&lt;answer&gt;.
/// </summary>
public sealed class HandResponseDecoderTests
{
    [Fact]
    public void Decode_CleanWireLine_ExtractsTextConfidenceLevel1()
    {
        HandDecodedResponse r = HandResponseDecoder.Decode("R|C=0.9|V=hello world", AgentClass.Assisted);

        r.Text.Should().Be("hello world");
        r.Confidence.Should().Be(0.9);
        r.HasCrisisSignal.Should().BeFalse();
        r.ResilienceLevel.Should().Be(1);
    }

    [Fact]
    public void Decode_WireLineWithBody_ConcatenatesVAndBody()
    {
        HandDecodedResponse r = HandResponseDecoder.Decode(
            "R|C=0.9|V=First line.\nSecond paragraph continues.", AgentClass.Assisted);

        r.Text.Should().Be("First line.\nSecond paragraph continues.");
        r.Confidence.Should().Be(0.9);
        r.ResilienceLevel.Should().Be(2); // multi-line → lenient parse (strict is single-line only)
    }

    [Fact]
    public void Decode_PrefillContinuation_ReattachesAndParses()
    {
        // Ollama returns only the continuation after a trailing "R|C=" prefill turn.
        HandDecodedResponse r = HandResponseDecoder.Decode("0.88|V=hello there", AgentClass.Assisted);

        r.Text.Should().Be("hello there");
        r.Confidence.Should().Be(0.88);
        r.ResilienceLevel.Should().Be(1);
    }

    [Fact]
    public void Decode_PlainProse_FailsOpen()
    {
        HandDecodedResponse r = HandResponseDecoder.Decode(
            "I hear you and I want to help you through this.", AgentClass.Assisted);

        r.Text.Should().Be("I hear you and I want to help you through this.");
        r.Confidence.Should().Be(0.5);
        r.ResilienceLevel.Should().Be(6);
    }

    [Fact]
    public void Decode_FreeFormProseWithHints_SalvagedBySemanticExtraction()
    {
        // No wire line at all — the resilience ladder's semantic stage recovers value + confidence.
        HandDecodedResponse r = HandResponseDecoder.Decode(
            "answer: the recovered text\nconfidence: 0.87", AgentClass.Assisted);

        r.Text.Should().Be("the recovered text");
        r.Confidence.Should().BeApproximately(0.87, 0.001);
        r.ResilienceLevel.Should().Be(4);
    }

    [Fact]
    public void Decode_CrisisSignal_DetectedFromSField()
    {
        HandDecodedResponse r = HandResponseDecoder.Decode(
            "R|C=0.8|S=crisis|V=I cannot go on", AgentClass.Assisted);

        r.HasCrisisSignal.Should().BeTrue();
        r.Text.Should().Be("I cannot go on");
    }

    [Fact]
    public void Decode_MarkdownFenceAroundWire_StrippedAndParsed()
    {
        HandDecodedResponse r = HandResponseDecoder.Decode(
            "```\nR|C=0.82|V=fenced answer\n```", AgentClass.Assisted);

        r.Text.Should().Be("fenced answer");
        r.Confidence.Should().Be(0.82);
    }

    [Fact]
    public void Decode_MissingConfidence_UsesFallback()
    {
        HandDecodedResponse r = HandResponseDecoder.Decode("R|V=no confidence here", AgentClass.Assisted);

        r.Text.Should().Be("no confidence here");
        r.Confidence.Should().Be(0.5);
    }

    [Fact]
    public void Decode_NativeClass_NoPrefillReattach()
    {
        HandDecodedResponse r = HandResponseDecoder.Decode("R|C=0.91|V=native answer", AgentClass.Native);

        r.Text.Should().Be("native answer");
        r.Confidence.Should().Be(0.91);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Decode_EmptyOrNull_ReturnsEmptyWithoutThrowing(string? input)
    {
        HandDecodedResponse r = HandResponseDecoder.Decode(input!, AgentClass.Assisted);

        r.Text.Should().BeEmpty();
        r.HasCrisisSignal.Should().BeFalse();
    }
}
