using FluentAssertions;
using HybridTherapist.Domain.Models;
using HybridTherapist.Infrastructure.State;
using Xunit;

namespace HybridTherapist.Tests;

public sealed class InMemoryStateRepositoryTests
{
    private readonly InMemoryTherapyStateRepository _repo = new();

    [Fact]
    public async Task GetAsync_NewSession_ReturnsInitialState()
    {
        var state = await _repo.GetAsync("new-session-123");

        state.Should().NotBeNull();
        state.SessionId.Should().Be("new-session-123");
        state.CurrentPhase.Should().Be("INIT");
        state.MessageCount.Should().Be(0);
        state.Topics.Should().BeEmpty();
        state.History.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_ReturnsSavedState()
    {
        var state = new TherapyConversationState
        {
            SessionId = "sess-001",
            CurrentPhase = "EXPLORATION",
            MessageCount = 5,
            History = [new ChatMessage { Role = "user", Content = "hello" }],
        };

        await _repo.SaveAsync(state);
        var loaded = await _repo.GetAsync("sess-001");

        loaded.CurrentPhase.Should().Be("EXPLORATION");
        loaded.MessageCount.Should().Be(5);
        loaded.History.Should().HaveCount(1);
        loaded.History[0].Content.Should().Be("hello");
    }

    [Fact]
    public async Task SaveAsync_OverwritesPreviousState()
    {
        var state1 = new TherapyConversationState { SessionId = "sess-002", CurrentPhase = "INIT" };
        var state2 = new TherapyConversationState { SessionId = "sess-002", CurrentPhase = "DIGGING", MessageCount = 10 };

        await _repo.SaveAsync(state1);
        await _repo.SaveAsync(state2);

        var loaded = await _repo.GetAsync("sess-002");
        loaded.CurrentPhase.Should().Be("DIGGING");
        loaded.MessageCount.Should().Be(10);
    }

    [Fact]
    public async Task GetAsync_MultipleSessions_AreIsolated()
    {
        var stateA = new TherapyConversationState { SessionId = "A", CurrentPhase = "WORKING" };
        var stateB = new TherapyConversationState { SessionId = "B", CurrentPhase = "CLOSING" };

        await _repo.SaveAsync(stateA);
        await _repo.SaveAsync(stateB);

        var loadedA = await _repo.GetAsync("A");
        var loadedB = await _repo.GetAsync("B");

        loadedA.CurrentPhase.Should().Be("WORKING");
        loadedB.CurrentPhase.Should().Be("CLOSING");
    }

    [Fact]
    public async Task GetAsync_ConcurrentAccess_DoesNotThrow()
    {
        var tasks = Enumerable.Range(0, 20).Select(i =>
            Task.Run(async () =>
            {
                var s = new TherapyConversationState { SessionId = $"concurrent-{i}", MessageCount = i };
                await _repo.SaveAsync(s);
                return await _repo.GetAsync($"concurrent-{i}");
            }));

        var results = await Task.WhenAll(tasks);
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }
}
