using FluentAssertions;
using HybridTherapist.Domain.Models;
using HybridTherapist.Domain.Services;
using Xunit;

namespace HybridTherapist.Tests;

public sealed class ResponseStrategySelectorTests
{
    [Theory]
    [InlineData("INIT", "low", false, ResponseStrategy.Intake)]
    [InlineData("INIT", "medium", false, ResponseStrategy.Intake)]
    [InlineData("INIT", "high", false, ResponseStrategy.Stabilizing)]
    [InlineData("EXPLORATION", "low", false, ResponseStrategy.Mapping)]
    [InlineData("EXPLORATION", "high", false, ResponseStrategy.MappingWithNaming)]
    [InlineData("DIGGING", "low", false, ResponseStrategy.Deepening)]
    [InlineData("DIGGING", "moderate", false, ResponseStrategy.DeepeningWithMech)]
    [InlineData("DIGGING", "high", false, ResponseStrategy.Stabilizing)]
    [InlineData("WORKING", "low", false, ResponseStrategy.Intervention)]
    [InlineData("WORKING", "moderate", false, ResponseStrategy.StabilizingIntervention)]
    [InlineData("WORKING", "high", false, ResponseStrategy.Stabilizing)]
    [InlineData("CLOSING", "low", false, ResponseStrategy.Closure)]
    public void Select_MapsPhaseAndSeverityToStrategy(string phase, string severity, bool rupture, ResponseStrategy expected)
    {
        ResponseStrategySelector.Select(phase, severity, rupture).Should().Be(expected);
    }

    [Theory]
    [InlineData("INIT", "low")]
    [InlineData("DIGGING", "high")]
    [InlineData("WORKING", "moderate")]
    public void Select_RuptureDetected_AlwaysReturnsRepair(string phase, string severity)
    {
        ResponseStrategySelector.Select(phase, severity, ruptureDetected: true)
            .Should().Be(ResponseStrategy.Repair);
    }

    [Fact]
    public void Select_UnknownPhase_FallsBackToIntake()
    {
        ResponseStrategySelector.Select("UNKNOWN_PHASE", "low").Should().Be(ResponseStrategy.Intake);
    }
}
