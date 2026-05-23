using HybridTherapist.Domain.Models;

namespace HybridTherapist.Domain.Interfaces;

public interface ITherapyConversationStateRepository
{
    Task<TherapyConversationState> GetAsync(string sessionId, CancellationToken ct = default);
    Task SaveAsync(TherapyConversationState state, CancellationToken ct = default);
}
