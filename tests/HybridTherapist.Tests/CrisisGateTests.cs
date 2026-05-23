using FluentAssertions;
using HybridTherapist.Security.Gates;
using Xunit;

namespace HybridTherapist.Tests;

/// <summary>
/// Both positive (crisis detected) and negative (safe input) tests required
/// by security-gates.md rules — a file with only passing cases is a violation.
/// </summary>
public sealed class CrisisGateTests
{
    private readonly CrisisGate _gate = new();

    // ── Hard-stop: positive cases ─────────────────────────────────────────────

    [Theory]
    [InlineData("chcę skończyć z sobą")]
    [InlineData("myślę o samobójstwie")]
    [InlineData("chcę się zabić")]
    [InlineData("I want to kill myself")]
    [InlineData("I'm thinking about suicide")]
    public void Check_CrisisInput_ReturnsHardStop(string input)
    {
        var result = _gate.Check(input);

        result.IsHardStop.Should().BeTrue($"'{input}' is a hard-stop crisis phrase");
        result.HardStopMessage.Should().NotBeNullOrEmpty("hard-stop must include helpline message");
        result.HardStopMessage.Should().Contain("116 123", "Polish helpline number must appear in response");
    }

    // ── Hard-stop: negative cases ─────────────────────────────────────────────

    [Theory]
    [InlineData("nie mogę zasnąć")]
    [InlineData("czuję się zestresowany pracą")]
    [InlineData("mam problem z koncentracją")]
    [InlineData("jestem zmęczony")]
    [InlineData("hello")]
    [InlineData("")]
    public void Check_SafeInput_DoesNotTriggerHardStop(string input)
    {
        var result = _gate.Check(input);

        result.IsHardStop.Should().BeFalse($"'{input}' is NOT a crisis phrase");
    }

    // ── Escalation: medium severity ───────────────────────────────────────────

    [Theory]
    [InlineData("nie mogę zasnąć")]
    [InlineData("ciągle zmęczony")]
    [InlineData("bezsenność mnie dobija")]
    public void Check_MediumSeverityInput_ReturnsEscalation(string input)
    {
        var result = _gate.Check(input);

        result.IsHardStop.Should().BeFalse();
        result.IsEscalation.Should().BeTrue();
        result.Severity.Should().Be("medium");
    }

    // ── Escalation: high severity ─────────────────────────────────────────────

    [Theory]
    [InlineData("nie daję rady już dłużej")]
    [InlineData("przytłoczona tym wszystkim")]
    [InlineData("nie widzę wyjścia")]
    public void Check_HighSeverityInput_ReturnsHighEscalation(string input)
    {
        var result = _gate.Check(input);

        result.IsHardStop.Should().BeFalse();
        result.IsEscalation.Should().BeTrue();
        result.Severity.Should().Be("high");
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void Check_Null_DoesNotThrow()
    {
        var result = _gate.Check(null!);
        result.IsHardStop.Should().BeFalse();
    }

    [Fact]
    public void Check_EmptyString_ReturnsSafe()
    {
        _gate.Check(string.Empty).IsHardStop.Should().BeFalse();
        _gate.Check("   ").IsHardStop.Should().BeFalse();
    }

    [Fact]
    public void Check_ProjectRelatedKoncz_IsNotCrisis()
    {
        // "chcę skończyć z tym projektem" — ending a project, not life
        var result = _gate.Check("chcę skończyć z tym projektem");
        result.IsHardStop.Should().BeFalse("finishing a project is not a crisis");
    }
}
