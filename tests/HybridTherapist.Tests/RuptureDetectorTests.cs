using FluentAssertions;
using HybridTherapist.Domain.Services;
using Xunit;

namespace HybridTherapist.Tests;

public sealed class RuptureDetectorTests
{
    private const string PriorAssistantTurn =
        "Rozumiem, że trudno Ci zasnąć. Może to być związane z lękiem.";

    // ── Positive cases (rupture detected) ────────────────────────────────────

    [Theory]
    [InlineData("nie, wcale nie o to chodziło")]
    [InlineData("źle mnie rozumiesz, to nie tak")]
    [InlineData("That's not what I meant at all")]
    [InlineData("you misunderstood me")]
    [InlineData("nie słuchasz mnie w ogóle")]
    [InlineData("you're not listening to me")]
    public void Check_UserCorrectionAfterAssistantTurn_DetectsRupture(string userMsg)
    {
        RuptureDetector.Result r = RuptureDetector.Check(userMsg, PriorAssistantTurn);
        r.Detected.Should().BeTrue();
        r.Reason.Should().NotBeNull();
    }

    // ── Negative cases (no rupture) ──────────────────────────────────────────

    [Fact]
    public void Check_FirstTurn_NoRupture()
    {
        // No prior assistant message → can't have a rupture
        RuptureDetector.Check("nie wiem co robić", lastAssistantMessage: null)
            .Detected.Should().BeFalse();
    }

    [Theory]
    [InlineData("tak, dokładnie tak się czuję")]
    [InlineData("dziękuję, to mi pomogło")]
    [InlineData("yes, that resonates with me")]
    [InlineData("opowiem ci więcej o tym")]
    public void Check_NormalContinuation_NoRupture(string userMsg)
    {
        RuptureDetector.Check(userMsg, PriorAssistantTurn).Detected.Should().BeFalse();
    }

    [Fact]
    public void Check_EmptyUserMessage_NoRupture()
    {
        RuptureDetector.Check("", PriorAssistantTurn).Detected.Should().BeFalse();
    }

    // ── New patterns (repeated frustration, being ignored) ────────────────────

    [Theory]
    [InlineData("znowu to samo, czy Ty w ogóle słuchasz?")]
    [InlineData("już mówiłem że nie o to chodzi")]
    [InlineData("dalej nie rozumiesz o co mi chodzi")]
    [InlineData("ignorujesz to co mówię")]
    [InlineData("powtarzasz się, nic nowego nie powiedziałeś")]
    [InlineData("nie odpowiedziałeś na moje pytanie")]
    [InlineData("gadam jak do ściany")]
    [InlineData("ta rozmowa nie ma sensu")]
    public void Check_RepeatedFrustration_DetectsRupture(string userMsg)
    {
        RuptureDetector.Result r = RuptureDetector.Check(userMsg, PriorAssistantTurn);
        r.Detected.Should().BeTrue();
    }
}
