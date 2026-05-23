using System.Collections.Concurrent;
using HybridTherapist.Domain.Interfaces;

namespace HybridTherapist.Infrastructure.Tracing;

/// <summary>
/// In-memory trace store with bounded per-session capacity and TTL eviction.
/// Thread-safe. Suitable for demo + single-instance deployments.
/// </summary>
public sealed class InMemoryTraceSink : ITraceSink
{
    private const int MaxEventsPerSession = 200;
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(2);

    private readonly ConcurrentDictionary<string, List<TraceEvent>> _store = new();

    public Task RecordAsync(TraceEvent evt, CancellationToken ct = default)
    {
        List<TraceEvent> events = _store.GetOrAdd(evt.SessionId, _ => []);
        lock (events)
        {
            events.Add(evt);
            // Drop oldest if over capacity
            if (events.Count > MaxEventsPerSession)
                events.RemoveRange(0, events.Count - MaxEventsPerSession);
        }

        EvictExpired();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TraceEvent>> GetAsync(string sessionId, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(sessionId, out List<TraceEvent>? events))
            return Task.FromResult<IReadOnlyList<TraceEvent>>(Array.Empty<TraceEvent>());

        lock (events)
        {
            return Task.FromResult<IReadOnlyList<TraceEvent>>(events.ToArray());
        }
    }

    public Task ClearAsync(string sessionId, CancellationToken ct = default)
    {
        _store.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    private void EvictExpired()
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow - Ttl;
        foreach (KeyValuePair<string, List<TraceEvent>> kvp in _store)
        {
            lock (kvp.Value)
            {
                if (kvp.Value.Count > 0 && kvp.Value[^1].Timestamp < cutoff)
                    _store.TryRemove(kvp.Key, out _);
            }
        }
    }
}
