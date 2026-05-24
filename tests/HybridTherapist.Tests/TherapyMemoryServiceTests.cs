using FluentAssertions;
using HybridTherapist.Application.Layers;
using HybridTherapist.Application.Options;
using HybridTherapist.Domain.Interfaces;
using HybridTherapist.Domain.Models;
using HybridTherapist.Infrastructure.Adapters;
using HybridTherapist.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

using static HybridTherapist.Application.Layers.TherapyMemoryService;

namespace HybridTherapist.Tests;

public sealed class TherapyMemoryServiceTests
{
    [Fact]
    public void ShouldSummarize_HistoryTooShort_False()
    {
        // 6 or fewer messages → don't bother summarizing
        var state = new TherapyConversationState
        {
            MessageCount = 6,
            History = Enumerable.Range(0, 6).Select(i => new ChatMessage { Role = "user", Content = $"m{i}" }).ToList(),
        };
        TherapyMemoryService.ShouldSummarize(state, phaseJustChanged: false).Should().BeFalse();
    }

    [Fact]
    public void ShouldSummarize_EveryEighthMessage_True()
    {
        var state = new TherapyConversationState
        {
            MessageCount = 8,
            History = Enumerable.Range(0, 10).Select(i => new ChatMessage { Role = "user", Content = $"m{i}" }).ToList(),
        };
        TherapyMemoryService.ShouldSummarize(state, phaseJustChanged: false).Should().BeTrue();
    }

    [Fact]
    public void ShouldSummarize_PhaseChange_TriggersEvenIfNotEighth()
    {
        var state = new TherapyConversationState
        {
            MessageCount = 5,
            History = Enumerable.Range(0, 10).Select(i => new ChatMessage { Role = "user", Content = $"m{i}" }).ToList(),
        };
        TherapyMemoryService.ShouldSummarize(state, phaseJustChanged: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldSummarize_NotEighthAndNoPhaseChange_False()
    {
        var state = new TherapyConversationState
        {
            MessageCount = 9,
            History = Enumerable.Range(0, 10).Select(i => new ChatMessage { Role = "user", Content = $"m{i}" }).ToList(),
        };
        TherapyMemoryService.ShouldSummarize(state, phaseJustChanged: false).Should().BeFalse();
    }

    [Fact]
    public void GetCompactionTier_PhaseChange_ReturnsPhase()
    {
        var tier = GetCompactionTier(messageCount: 5, phaseJustChanged: true);
        tier.Should().Be(CompactionTier.Phase);
    }

    [Fact]
    public void GetCompactionTier_Every24thMessage_ReturnsDeep()
    {
        var tier = GetCompactionTier(messageCount: 24, phaseJustChanged: false);
        tier.Should().Be(CompactionTier.Deep);
    }

    [Fact]
    public void GetCompactionTier_Every8thNot24th_ReturnsStandard()
    {
        var tier = GetCompactionTier(messageCount: 8, phaseJustChanged: false);
        tier.Should().Be(CompactionTier.Standard);
    }

    [Fact]
    public void GetCompactionTier_Message16_ReturnsStandard()
    {
        var tier = GetCompactionTier(messageCount: 16, phaseJustChanged: false);
        tier.Should().Be(CompactionTier.Standard);
    }

    [Fact]
    public void GetCompactionTier_PhaseTakesPriorityOverDeep()
    {
        var tier = GetCompactionTier(messageCount: 24, phaseJustChanged: true);
        tier.Should().Be(CompactionTier.Phase);
    }

    [Fact]
    public void GetCompactionTier_Message0_NoPhase_ReturnsStandard()
    {
        var tier = GetCompactionTier(messageCount: 0, phaseJustChanged: false);
        tier.Should().Be(CompactionTier.Standard);
    }

    [Fact]
    public void GetCompactionTier_Message48_ReturnsDeep()
    {
        var tier = GetCompactionTier(messageCount: 48, phaseJustChanged: false);
        tier.Should().Be(CompactionTier.Deep);
    }

    private static TherapyMemoryService CreateMemoryService(string fakeOutput)
    {
        var fakeAdapter = new FakeOllamaAdapter(fakeOutput);
        var opts = Options.Create(new TherapistOptions());
        var trace = new NullTraceSink();
        return new TherapyMemoryService(
            fakeAdapter, opts, trace, NullLogger<TherapyMemoryService>.Instance);
    }

    private static TherapyMemoryService CreateMemoryService(LlmResponse fakeResponse)
    {
        var fakeAdapter = new FakeOllamaAdapter(fakeResponse);
        var opts = Options.Create(new TherapistOptions());
        var trace = new NullTraceSink();
        return new TherapyMemoryService(
            fakeAdapter, opts, trace, NullLogger<TherapyMemoryService>.Instance);
    }

    [Fact]
    public async Task SummarizeAndCompact_StandardTier_ParsesAndStoresSummary()
    {
        string fakeOutput = """
            [OVERVIEW]
            User presents with moderate anxiety about work.

            [TOPIC MAP]
            work_stress: msg1→msg7 | evolution: deadlines→anxiety | status: active

            [EMOTIONAL ARC]
            anxiety(medium, msg1-7)
            """;

        var service = CreateMemoryService(fakeOutput);
        var state = new TherapyConversationState
        {
            SessionId = "sess_test",
            MessageCount = 8,
            History = Enumerable.Range(0, 10).Select(i =>
                new ChatMessage { Role = "user", Content = $"message {i}" }).ToList(),
        };

        var result = await service.SummarizeAndCompactAsync(
            "sess_test", state, CompactionTier.Standard);

        result.Should().NotBeNull();
        result!.Overview.Should().Contain("anxiety");
        result.TopicMap.Should().HaveCount(1);
        result.TopicMap[0].Theme.Should().Be("work_stress");
        result.EmotionalArc.Should().Contain("anxiety");
        state.StructuredSummary.Should().NotBeNull();
        state.SessionSummary.Should().Contain("anxiety");
        state.History.Should().HaveCount(6,
            "history should be truncated to last 6 messages");
    }

    [Fact]
    public async Task SummarizeAndCompact_FailedLlmCall_ReturnsPreviousSummary()
    {
        var previousSummary = new MemorySummary(
            "Old overview", Array.Empty<TopicEntry>(), "stable", null, null);

        var state = new TherapyConversationState
        {
            SessionId = "sess_test",
            MessageCount = 8,
            StructuredSummary = previousSummary,
            History = Enumerable.Range(0, 10).Select(i =>
                new ChatMessage { Role = "user", Content = $"message {i}" }).ToList(),
        };

        var errorResponse = new LlmResponse { Ok = false, Error = "Ollama timeout" };
        var service = CreateMemoryService(errorResponse);

        var result = await service.SummarizeAndCompactAsync(
            "sess_test", state, CompactionTier.Standard);

        result.Should().BeSameAs(previousSummary,
            "should return existing summary on LLM failure");
        state.History.Should().HaveCount(10,
            "history should NOT be truncated on failure");
    }

    [Fact]
    public async Task SummarizeAndCompact_UnparseableOutput_ReturnsPreviousSummary()
    {
        string garbageOutput = "I am a helpful assistant. Here is the summary: ...";

        var previousSummary = new MemorySummary(
            "Old overview", Array.Empty<TopicEntry>(), "stable", null, null);

        var state = new TherapyConversationState
        {
            SessionId = "sess_test",
            MessageCount = 8,
            StructuredSummary = previousSummary,
            History = Enumerable.Range(0, 10).Select(i =>
                new ChatMessage { Role = "user", Content = $"message {i}" }).ToList(),
        };

        var service = CreateMemoryService(garbageOutput);

        var result = await service.SummarizeAndCompactAsync(
            "sess_test", state, CompactionTier.Standard);

        result.Should().BeSameAs(previousSummary,
            "should keep existing summary when parser can't parse output");
        state.History.Should().HaveCount(10,
            "history should NOT be truncated on parse failure");
    }

    [Fact]
    public async Task SummarizeAndCompact_HistoryTooShort_NoOp()
    {
        var service = CreateMemoryService("irrelevant");

        var state = new TherapyConversationState
        {
            SessionId = "sess_test",
            MessageCount = 6,
            History = Enumerable.Range(0, 6).Select(i =>
                new ChatMessage { Role = "user", Content = $"message {i}" }).ToList(),
        };

        var result = await service.SummarizeAndCompactAsync(
            "sess_test", state, CompactionTier.Standard);

        result.Should().BeNull("no structured summary existed yet");
        state.History.Should().HaveCount(6, "history too short to truncate");
    }

    [Fact]
    public async Task SummarizeAndCompact_DeepTier_IncludesFlagsAndFocus()
    {
        string fakeOutput = """
            [OVERVIEW]
            Deep review summary.

            [TOPIC MAP]
            anxiety: msg1→msg22 | evolution: escalating→slight_improvement | status: active

            [EMOTIONAL ARC]
            anxiety(high, msg1-10) → cautious_hope(low, msg11+)

            [CLINICAL FLAGS]
            CONTRADICTION: user claims progress but describes worsening symptoms
            STUCK: reframing attempts have not worked in 4 attempts

            [FOCUS NEXT]
            Try grounding techniques instead of cognitive reframing.
            """;

        var service = CreateMemoryService(fakeOutput);
        var state = new TherapyConversationState
        {
            SessionId = "sess_test",
            MessageCount = 24,
            History = Enumerable.Range(0, 30).Select(i =>
                new ChatMessage { Role = "user", Content = $"message {i}" }).ToList(),
        };

        var result = await service.SummarizeAndCompactAsync(
            "sess_test", state, CompactionTier.Deep);

        result.Should().NotBeNull();
        result!.ClinicalFlags.Should().Contain("CONTRADICTION");
        result.ClinicalFlags.Should().Contain("STUCK");
        result.FocusNext.Should().Contain("grounding");
        state.History.Should().HaveCount(6);
    }

    // ── DetectTrend ───────────────────────────────────────────────────────────

    [Fact]
    public void DetectTrend_NoPrevious_ReturnsStable()
    {
        var current = new MemorySummary("overview1", Array.Empty<TopicEntry>(), "low", null, null);
        var result = TherapyMemoryService.DetectTrend(current, null);
        result.Should().Be("stable");
    }

    [Fact]
    public void DetectTrend_Worsening_LowToHigh()
    {
        var prev = new MemorySummary("overview1", Array.Empty<TopicEntry>(), "low", null, null);
        var curr = new MemorySummary("overview2", Array.Empty<TopicEntry>(), "high", null, null);
        var result = TherapyMemoryService.DetectTrend(curr, prev);
        result.Should().Be("worsening");
    }

    [Fact]
    public void DetectTrend_Improving_HighToLow()
    {
        var prev = new MemorySummary("overview1", Array.Empty<TopicEntry>(), "high", null, null);
        var curr = new MemorySummary("overview2", Array.Empty<TopicEntry>(), "low", null, null);
        var result = TherapyMemoryService.DetectTrend(curr, prev);
        result.Should().Be("improving");
    }

    [Fact]
    public void DetectTrend_Stable_BothLow()
    {
        var prev = new MemorySummary("overview1", Array.Empty<TopicEntry>(), "low", null, null);
        var curr = new MemorySummary("overview2", Array.Empty<TopicEntry>(), "low", null, null);
        var result = TherapyMemoryService.DetectTrend(curr, prev);
        result.Should().Be("stable");
    }

    [Fact]
    public void DetectTrend_CrisisDetection()
    {
        var prev = new MemorySummary("overview1", Array.Empty<TopicEntry>(), "low → moderate", null, null);
        var curr = new MemorySummary("overview2", Array.Empty<TopicEntry>(), "crisis", null, null);
        var result = TherapyMemoryService.DetectTrend(curr, prev);
        result.Should().Be("worsening");
    }
}
