namespace HybridTherapist.Domain.Models;

/// <summary>
/// Structured analyst output. Parsed from the L2 model's text response and
/// re-encoded as <c>M|</c> Memo wire format for L3/L4 consumption.
/// </summary>
public sealed record ClinicalReport(
    string EmotionalState,
    IReadOnlyList<string> RiskIndicators,
    IReadOnlyList<string> CognitivePatterns,
    IReadOnlyList<string> EvidenceQuotes,
    ClinicalSeverity Severity);

public enum ClinicalSeverity { Low, Moderate, High, Crisis }

/// <summary>
/// Structured supervisor output. The supervisor reads the analyst's Memo,
/// picks an approach/technique, and emits a new <c>M|</c> Memo for L4.
/// </summary>
public sealed record TherapeuticPlan(
    string Approach,
    string Technique,
    string KeyQuestion,
    string? RiskNote);

/// <summary>
/// 10 response strategies, picked by phase × severity in <c>ResponseStrategySelector</c>.
/// Cortexa parity: <c>Cortexa.Orchestrator.Domain.Models.Therapy.ResponseStrategy</c>.
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
