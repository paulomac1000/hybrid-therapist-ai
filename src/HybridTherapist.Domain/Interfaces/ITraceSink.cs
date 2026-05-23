namespace HybridTherapist.Domain.Interfaces;

/// <summary>
/// Captures per-layer trace events for a therapy session. Used to debug what each
/// layer received, what it emitted, how long it took, and which model handled it.
/// Cortexa parity: <c>IAuditRepository</c> wrote similar events to SQLite; here we
/// keep them in memory + expose via <c>/v1/trace/{sessionId}</c>.
/// </summary>
public interface ITraceSink
{
    /// <summary>Records a single layer execution event for a session.</summary>
    Task RecordAsync(TraceEvent evt, CancellationToken ct = default);

    /// <summary>Reads all events for a session in chronological order.</summary>
    Task<IReadOnlyList<TraceEvent>> GetAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Clears trace for a session (manual / TTL eviction).</summary>
    Task ClearAsync(string sessionId, CancellationToken ct = default);
}

/// <summary>
/// A single layer execution record. Immutable.
/// </summary>
public sealed record TraceEvent(
    DateTimeOffset Timestamp,
    string SessionId,
    string Layer,
    string? Model,
    string Input,
    string Output,
    long DurationMs,
    string Outcome,
    string? Error = null,
    string? WireFormat = null);
