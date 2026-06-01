using HybridTherapist.Domain.Models;

namespace HybridTherapist.Domain.Services;

/// <summary>
/// Picks a <see cref="ResponseStrategy"/> from phase × severity × rupture.
/// Selects the response strategy for the current phase, severity and rupture state.
/// </summary>
public static class ResponseStrategySelector
{
    public static ResponseStrategy Select(string phase, string severity, bool ruptureDetected = false)
    {
        if (ruptureDetected) return ResponseStrategy.Repair;

        bool high = string.Equals(severity, "high", StringComparison.OrdinalIgnoreCase)
            || string.Equals(severity, "crisis", StringComparison.OrdinalIgnoreCase);
        bool moderate = string.Equals(severity, "moderate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(severity, "medium", StringComparison.OrdinalIgnoreCase);

        return phase.ToUpperInvariant() switch
        {
            "INIT" => high ? ResponseStrategy.Stabilizing : (moderate ? ResponseStrategy.Mapping : ResponseStrategy.Intake),
            "EXPLORATION" => high ? ResponseStrategy.MappingWithNaming : ResponseStrategy.Mapping,
            "DIGGING" => high
                ? ResponseStrategy.Stabilizing
                : (moderate ? ResponseStrategy.DeepeningWithMech : ResponseStrategy.Deepening),
            "WORKING" => high
                ? ResponseStrategy.Stabilizing
                : (moderate ? ResponseStrategy.StabilizingIntervention : ResponseStrategy.Intervention),
            "CLOSING" => ResponseStrategy.Closure,
            _ => ResponseStrategy.Intake,
        };
    }
}
