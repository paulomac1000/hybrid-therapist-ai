using FluentAssertions;
using HybridTherapist.Domain.Services;
using Xunit;

namespace HybridTherapist.Tests;

public sealed class ThematicAlignmentTests
{
    [Fact]
    public void Verify_AnalystMatchesUserInput_Aligned()
    {
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "User reports betrayal by their spouse",
            userInput: "mój mąż mnie zdradził, czuję się okropnie");
        r.Aligned.Should().BeTrue();
        r.UnsupportedThemes.Should().BeEmpty();
    }

    [Fact]
    public void Verify_AnalystInventsBetrayalNotInUserInput_NotAligned()
    {
        // User says "I can't sleep". Analyst inserts "betrayal" — pure hallucination.
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "Themes: betrayal, broken trust, marital infidelity",
            userInput: "nie mogę zasnąć od trzech tygodni");
        r.Aligned.Should().BeFalse();
        r.UnsupportedThemes.Should().Contain("betrayal");
    }

    [Fact]
    public void Verify_AnalystInventsAbuse_NotAligned()
    {
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "Indicators of past abuse and trauma",
            userInput: "czuję się zmęczona w pracy");
        r.Aligned.Should().BeFalse();
        r.UnsupportedThemes.Should().Contain("abuse");
    }

    [Fact]
    public void Verify_AnalystMentionsSuicideWhenUserDid_Aligned()
    {
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "Severe — suicidal ideation indicated",
            userInput: "myślę o samobójstwie");
        r.Aligned.Should().BeTrue();
    }

    [Fact]
    public void Verify_NoSensitiveThemes_Aligned()
    {
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "Mild work stress, fatigue",
            userInput: "praca mnie męczy");
        r.Aligned.Should().BeTrue();
    }

    [Fact]
    public void Verify_EmptyInputs_AlignedByDefault()
    {
        ThematicAlignment.Verify("", "anything").Aligned.Should().BeTrue();
        ThematicAlignment.Verify("anything", "").Aligned.Should().BeTrue();
    }

    [Fact]
    public void Verify_RawMWire_BetrayalNotInUserInput_NotAligned()
    {
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "M|L=2|em=betrayal|sv=moderate|ri=broken_trust|cp=catastrophizing",
            userInput: "nie mogę zasnąć od trzech tygodni");
        r.Aligned.Should().BeFalse();
        r.UnsupportedThemes.Should().Contain("betrayal");
    }

    [Fact]
    public void Verify_RawMWire_BetrayalSupportedByUserInput_Aligned()
    {
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "M|L=2|em=betrayal|sv=high|ri=anger",
            userInput: "mąż mnie zdradził, nie mogę w to uwierzyć");
        r.Aligned.Should().BeTrue();
        r.UnsupportedThemes.Should().BeEmpty();
    }

    [Fact]
    public void Verify_RawMWire_SuicideFabricated_NotAligned()
    {
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "M|L=2|em=depression|sv=crisis|ri=suicidal|cp=self-harm",
            userInput: "nie mogę zasnąć, jestem zmęczona");
        r.Aligned.Should().BeFalse();
        r.UnsupportedThemes.Should().Contain("suicide");
    }

    [Fact]
    public void Verify_RawMWire_NoSensitiveThemes_Aligned()
    {
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "M|L=2|em=exhaustion|sv=moderate|ri=insomnia|cp=worry",
            userInput: "nie mogę zasnąć od trzech tygodni");
        r.Aligned.Should().BeTrue();
        r.UnsupportedThemes.Should().BeEmpty();
    }
}
