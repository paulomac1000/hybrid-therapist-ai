namespace HybridTherapist.Domain.Models;

/// <summary>
/// 10 response strategies, picked by phase × severity in <c>ResponseStrategySelector</c>.
/// Pipeline strategy enum used by the Socrates orchestration layer.
/// </summary>
public enum ResponseStrategy
{
    Intake,                  // INIT + low/medium severity
    Mapping,                 // EXPLORATION + low/medium
    MappingWithNaming,       // EXPLORATION + high severity (no crisis)
    Deepening,               // DIGGING + low
    DeepeningWithMech,       // DIGGING + moderate
    Intervention,            // WORKING + low
    StabilizingIntervention, // WORKING + moderate
    Stabilizing,             // any phase + high (not hard-stop)
    Closure,                 // CLOSING
    Repair,                  // any phase + rupture detected
}
