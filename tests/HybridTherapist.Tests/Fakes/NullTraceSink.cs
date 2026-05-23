using HybridTherapist.Domain.Interfaces;

namespace HybridTherapist.Tests.Fakes;

public sealed class NullTraceSink : ITraceSink
{
    public Task RecordAsync(TraceEvent evt, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<TraceEvent>> GetAsync(string sessionId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TraceEvent>>(Array.Empty<TraceEvent>());

    public Task ClearAsync(string sessionId, CancellationToken ct = default) => Task.CompletedTask;
}
