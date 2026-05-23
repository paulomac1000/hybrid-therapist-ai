using HybridTherapist.Domain.Models;

namespace HybridTherapist.Domain.Interfaces;

public interface ITherapistFlow
{
    Task<FlowExecutionResult> ExecuteAsync(ChatCompletionRequest request, CancellationToken ct = default);
}
