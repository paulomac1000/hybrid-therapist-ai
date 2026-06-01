using System.Collections.Concurrent;
using HybridTherapist.Domain.Interfaces;
using HybridTherapist.Domain.Models;

namespace HybridTherapist.Infrastructure.State;

/// <summary>
/// In-memory state repository for demo and benchmarks.
/// Thread-safe. All state is lost on restart — SQLite optional in production.
/// </summary>
public sealed class InMemoryTherapyStateRepository : ITherapyConversationStateRepository
{
    private readonly ConcurrentDictionary<string, TherapyConversationState> _store = new();

    public Task<TherapyConversationState> GetAsync(string sessionId, CancellationToken ct = default)
    {
        TherapyConversationState state = _store.GetOrAdd(sessionId, _ => new TherapyConversationState
        {
            SessionId = sessionId,
            CurrentPhase = "INIT",
            Topics = [],
            History = [],
        });

        return Task.FromResult(state);
    }

    public Task SaveAsync(TherapyConversationState state, CancellationToken ct = default)
    {
        _store[state.SessionId] = state;
        return Task.CompletedTask;
    }
}
