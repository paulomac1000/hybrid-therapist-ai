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

    // ── New categories ────────────────────────────────────────────────────────

    [Fact]
    public void Verify_SelfHarm_Fabricated_NotAligned()
    {
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "M|L=2|em=depressed|sv=high|ri=self-harm|cp=hopelessness",
            userInput: "jestem smutny i nie mam energii");
        r.Aligned.Should().BeFalse();
        r.UnsupportedThemes.Should().Contain("self_harm");
    }

    [Fact]
    public void Verify_SelfHarm_SupportedByUserInput_Aligned()
    {
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "M|L=2|em=crisis|sv=high|ri=self-harm,cutting",
            userInput: "czasem się okaleczam, nie wiem co robić");
        r.Aligned.Should().BeTrue();
    }

    [Fact]
    public void Verify_EatingDisorder_Fabricated_NotAligned()
    {
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "M|L=2|em=shame|sv=moderate|ri=binge,eating_disorder|cp=body_image",
            userInput: "czuję się zestresowany w pracy");
        r.Aligned.Should().BeFalse();
        r.UnsupportedThemes.Should().Contain("eating_disorder");
    }

    [Fact]
    public void Verify_EatingDisorder_SupportedByUserInput_Aligned()
    {
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "M|L=2|em=shame|sv=moderate|ri=binge|cp=control_loss",
            userInput: "nie mogę przestać jeść, wymiotuję po posiłkach");
        r.Aligned.Should().BeTrue();
    }

    [Fact]
    public void Verify_Psychosis_Fabricated_NotAligned()
    {
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "M|L=2|em=fear|sv=high|ri=psychosis,hallucinations|cp=paranoia",
            userInput: "boję się, że nie zdam egzaminu");
        r.Aligned.Should().BeFalse();
        r.UnsupportedThemes.Should().Contain("psychosis");
    }

    [Fact]
    public void Verify_Psychosis_SupportedByUserInput_Aligned()
    {
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "M|L=2|em=fear|sv=high|ri=psychosis,voices",
            userInput: "słyszę głosy które mówią mi co mam robić");
        r.Aligned.Should().BeTrue();
    }

    // ── Regression: ambiguous substrings must NOT support sensitive themes ────

    [Fact]
    public void Verify_SelfHarm_AmbiguousPhrase_NotAligned()
    {
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "M|L=2|em=neutral|sv=low|ri=self-harm,cutting",
            userInput: "chcę skończyć ten projekt, jestem zmęczony");
        r.Aligned.Should().BeFalse();
        r.UnsupportedThemes.Should().Contain("self_harm");
    }

    [Fact]
    public void Verify_EatingDisorder_AmbiguousPhrase_NotAligned()
    {
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "M|L=2|em=neutral|sv=low|ri=eating_disorder,binge",
            userInput: "lubię jeść i gotować, to mnie relaksuje");
        r.Aligned.Should().BeFalse();
        r.UnsupportedThemes.Should().Contain("eating_disorder");
    }

    [Fact]
    public void Verify_Suicide_AmbiguousPhrase_NotAligned()
    {
        var r = ThematicAlignment.Verify(
            analystMemoOrReport: "M|L=2|em=neutral|sv=low|ri=suicidal",
            userInput: "chcę skończyć studia i znaleźć pracę");
        r.Aligned.Should().BeFalse();
        r.UnsupportedThemes.Should().Contain("suicide");
    }
}
