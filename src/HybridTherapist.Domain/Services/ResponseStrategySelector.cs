using HybridTherapist.Domain.Models;
using static System.StringComparison;

namespace HybridTherapist.Domain.Services;

public static class ResponseStrategySelector
{
    public static ResponseStrategy Select(string phase, string severity, bool ruptureDetected = false)
    {
        if (ruptureDetected) return ResponseStrategy.Repair;

        bool high = IsHigh(severity);
        bool moderate = IsModerate(severity);
        return MapStrategy(phase, high, moderate);
    }

    private static bool IsHigh(string s) =>
        string.Equals(s, "high", OrdinalIgnoreCase) || string.Equals(s, "crisis", OrdinalIgnoreCase);

    private static bool IsModerate(string s) =>
        string.Equals(s, "moderate", OrdinalIgnoreCase) || string.Equals(s, "medium", OrdinalIgnoreCase);

    private static ResponseStrategy MapStrategy(string phase, bool high, bool moderate) => phase.ToUpperInvariant() switch
    {
        "INIT" => Resolve(ResponseStrategy.Intake, ResponseStrategy.Mapping, ResponseStrategy.Stabilizing, high, moderate),
        "EXPLORATION" => high ? ResponseStrategy.MappingWithNaming : ResponseStrategy.Mapping,
        "DIGGING" => Resolve(ResponseStrategy.Deepening, ResponseStrategy.DeepeningWithMech, ResponseStrategy.Stabilizing, high, moderate),
        "WORKING" => Resolve(ResponseStrategy.Intervention, ResponseStrategy.StabilizingIntervention, ResponseStrategy.Stabilizing, high, moderate),
        "CLOSING" => ResponseStrategy.Closure,
        _ => ResponseStrategy.Intake,
    };

#pragma warning disable S3358 // Intentional domain logic — severity × phase decision matrix
    private static ResponseStrategy Resolve(ResponseStrategy low, ResponseStrategy moderate, ResponseStrategy high,
        bool isHigh, bool isModerate) =>
        isHigh ? high : isModerate ? moderate : low;
#pragma warning restore S3358
}
