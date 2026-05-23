using FluentAssertions;
using HybridTherapist.Domain.Services;
using Xunit;

namespace HybridTherapist.Tests;

public sealed class SessionPhaseTests
{
    [Theory]
    [InlineData("INIT")]
    [InlineData("EXPLORATION")]
    [InlineData("DIGGING")]
    [InlineData("WORKING")]
    [InlineData("CLOSING")]
    public void GetCalibratorPhaseGuidance_AllPhases_ReturnsNonEmptyString(string phase)
    {
        var guidance = SessionPhase.GetCalibratorPhaseGuidance(phase);
        guidance.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetCalibratorPhaseGuidance_InitPhase_ContainsWarmthHint()
    {
        var guidance = SessionPhase.GetCalibratorPhaseGuidance("INIT");
        guidance.Should().Contain("warmth");
    }

    [Theory]
    [InlineData("INIT")]
    [InlineData("EXPLORATION")]
    [InlineData("DIGGING")]
    [InlineData("WORKING")]
    [InlineData("CLOSING")]
    public void GetCalibratorPhaseGuidance_AllPhases_ContainAntiFormulaicPhrase(string phase)
    {
        var guidance = SessionPhase.GetCalibratorPhaseGuidance(phase);
        guidance.Should().Contain("formulaic",
            "phase {0} should warn against formulaic openings", phase);
    }

    [Fact]
    public void GetCalibratorPhaseGuidance_UnknownPhase_ReturnsGeneric()
    {
        var guidance = SessionPhase.GetCalibratorPhaseGuidance("UNKNOWN");
        guidance.Should().NotBeNullOrWhiteSpace();
        guidance.Should().Contain("formulaic");
    }
}
