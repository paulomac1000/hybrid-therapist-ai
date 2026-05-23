using HybridTherapist.Domain.Interfaces;

namespace HybridTherapist.Api.Endpoints;

/// <summary>
/// Debug endpoints that expose the per-layer execution trace for a session.
/// Cortexa parity: <c>IAuditRepository</c> wrote similar events to SQLite.
/// </summary>
public static class TraceEndpoints
{
    public static void MapTraceEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/trace/{sessionId}", async (string sessionId, ITraceSink trace, CancellationToken ct) =>
        {
            IReadOnlyList<TraceEvent> events = await trace.GetAsync(sessionId, ct);
            return Results.Ok(new
            {
                session_id = sessionId,
                event_count = events.Count,
                events = events.Select(e => new
                {
                    timestamp = e.Timestamp,
                    layer = e.Layer,
                    model = e.Model,
                    duration_ms = e.DurationMs,
                    outcome = e.Outcome,
                    error = e.Error,
                    input = e.Input,
                    output = e.Output,
                    wire_format = e.WireFormat,
                }),
            });
        }).WithName("GetTrace");

        app.MapDelete("/v1/trace/{sessionId}", async (string sessionId, ITraceSink trace, CancellationToken ct) =>
        {
            await trace.ClearAsync(sessionId, ct);
            return Results.NoContent();
        }).WithName("ClearTrace");
    }
}
