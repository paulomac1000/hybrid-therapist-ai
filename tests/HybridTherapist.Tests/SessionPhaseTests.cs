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

    // ── Phase transitions: severity-aware ─────────────────────────────────────

    [Fact]
    public void Evaluate_INIT_WithLowSeverity_TransitionsAfter2Messages()
    {
        SessionPhase.Evaluate("INIT", 1, "low").Should().Be("INIT");
        SessionPhase.Evaluate("INIT", 2, "low").Should().Be("EXPLORATION");
    }

    [Theory]
    [InlineData("moderate")]
    [InlineData("high")]
    [InlineData("crisis")]
    public void Evaluate_INIT_WithElevatedSeverity_TransitionsAfter1Message(string severity)
    {
        SessionPhase.Evaluate("INIT", 1, severity).Should().Be("EXPLORATION");
    }

    [Fact]
    public void Evaluate_EXPLORATION_WithLowSeverity_TransitionsAfter6Messages()
    {
        SessionPhase.Evaluate("EXPLORATION", 5, "low").Should().Be("EXPLORATION");
        SessionPhase.Evaluate("EXPLORATION", 6, "low").Should().Be("DIGGING");
    }

    [Fact]
    public void Evaluate_EXPLORATION_WithHighSeverity_TransitionsAfter4Messages()
    {
        SessionPhase.Evaluate("EXPLORATION", 3, "high").Should().Be("EXPLORATION");
        SessionPhase.Evaluate("EXPLORATION", 4, "high").Should().Be("DIGGING");
    }

    [Fact]
    public void Evaluate_BackwardCompatible_DefaultSeverityIsLow()
    {
        SessionPhase.Evaluate("INIT", 2).Should().Be("EXPLORATION");
        SessionPhase.Evaluate("EXPLORATION", 6).Should().Be("DIGGING");
    }
}
