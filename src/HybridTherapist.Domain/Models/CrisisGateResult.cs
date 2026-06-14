namespace HybridTherapist.Domain.Models;

public sealed class CrisisGateResult
{
    public bool IsHardStop { get; init; }
    public bool IsEscalation { get; init; }
    public string Severity { get; init; } = "safe";
    public string? HardStopMessage { get; init; }

    public static CrisisGateResult Safe { get; } = new();

    public static CrisisGateResult HardStop(string message) =>
        new() { IsHardStop = true, Severity = "critical", HardStopMessage = message };

    public static CrisisGateResult Escalation(string severity) =>
        new() { IsEscalation = true, Severity = severity };
}
