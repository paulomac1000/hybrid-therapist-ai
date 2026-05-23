using FluentAssertions;
using HandCodec.Models;
using HybridTherapist.Application.Flows;
using HybridTherapist.Application.Hand;
using HybridTherapist.Application.Options;
using HybridTherapist.Domain.Interfaces;
using HybridTherapist.Domain.Models;
using HybridTherapist.Infrastructure.Adapters;
using HybridTherapist.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HybridTherapist.Tests;

public sealed class TherapistLayerServiceTests
{
    private static readonly TherapistOptions _opts = new()
    {
        Translator = "bielik",
        Analyst = "MentaLLaMA",
        Supervisor = "PsyLLM",
        Therapist = "PsychoCounsel",
        Calibrator = "dolphin",
        AgentClass = AgentClass.Assisted,
    };

    private static TherapistLayerService MakeService(IOllamaAdapter ollama) =>
        new(ollama, Microsoft.Extensions.Options.Options.Create(_opts), new NullTraceSink(),
            NullLogger<TherapistLayerService>.Instance);

    private static MemorySummary CreateSummary(
        string? clinicalFlags = null,
        string? focusNext = null)
    {
        var topics = new List<TopicEntry>
        {
            new("anxiety", "msg1→msg10", "escalating→improving", "active"),
            new("insomnia", "msg1→msg5", "persistent", "active"),
            new("coping", "msg6→msg10", "exploring", "dormant"),
        };
        return new MemorySummary(
            "Session about work anxiety and sleep problems.",
            topics,
            "anxiety(high, msg1-5) → cautious_hope(low, msg6+)",
            clinicalFlags,
            focusNext);
    }

    [Fact]
    public void BuildMemoryBlock_InitPhase_OnlyOverview()
    {
        var ms = CreateSummary(
            clinicalFlags: "STUCK: reframing fails",
            focusNext: "Try grounding");

        string result = TherapistLayerService.BuildMemoryBlock(ms, "INIT");

        result.Should().Contain("[SESSION OVERVIEW]");
        result.Should().NotContain("[DISCUSSED TOPICS]");
        result.Should().NotContain("[CLINICAL FLAGS]");
        result.Should().NotContain("[SUGGESTED FOCUS]");
        result.Should().NotContain("STUCK",
            "clinical flags should NOT appear in INIT phase");
    }

    [Fact]
    public void BuildMemoryBlock_DiggingPhase_IncludesFlagsAndFocusAndTopicDetail()
    {
        var ms = CreateSummary(
            clinicalFlags: "CONTRADICTION: says fine but looks stressed\nSTUCK: perfectionism",
            focusNext: "Explore perfectionism origins");

        string result = TherapistLayerService.BuildMemoryBlock(ms, "DIGGING");

        result.Should().Contain("[SESSION OVERVIEW]");
        result.Should().Contain("[TOPIC DETAIL]");
        result.Should().Contain("escalating→improving");
        result.Should().Contain("[CLINICAL FLAGS — PAY ATTENTION]");
        result.Should().Contain("CONTRADICTION");
        result.Should().Contain("[SUGGESTED FOCUS]");
        result.Should().Contain("perfectionism");
    }

    [Fact]
    public void BuildMemoryBlock_WorkingPhase_FiltersActiveTopicsOnly()
    {
        var ms = CreateSummary(
            focusNext: "Practice grounding daily");

        string result = TherapistLayerService.BuildMemoryBlock(ms, "WORKING");

        result.Should().Contain("[ACTIVE TOPICS]");
        result.Should().Contain("anxiety");
        result.Should().Contain("insomnia");
        result.Should().NotContain("coping",
            "dormant topic should NOT appear in WORKING phase");
        result.Should().Contain("[SUGGESTED FOCUS]");
        result.Should().Contain("grounding");
    }

    [Fact]
    public void BuildMemoryBlock_NullSummary_ReturnsEmptyString()
    {
        string result = TherapistLayerService.BuildMemoryBlock(null, "DIGGING");
        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildMemoryBlock_NoFlags_DoesNotIncludeClinicalSection()
    {
        var ms = CreateSummary(clinicalFlags: null, focusNext: null);

        string result = TherapistLayerService.BuildMemoryBlock(ms, "DIGGING");

        result.Should().NotContain("[CLINICAL FLAGS]");
        result.Should().NotContain("[SUGGESTED FOCUS]");
    }

    [Fact]
    public void BuildMemoryBlock_ExplorationPhase_IncludesTopicListAndEmotionalArc()
    {
        var ms = CreateSummary();

        string result = TherapistLayerService.BuildMemoryBlock(ms, "EXPLORATION");

        result.Should().Contain("[DISCUSSED TOPICS]");
        result.Should().Contain("anxiety (active)");
        result.Should().Contain("insomnia (active)");
        result.Should().Contain("coping (dormant)");
        result.Should().Contain("[EMOTIONAL ARC]");
        result.Should().Contain("cautious_hope");
        result.Should().NotContain("CLINICAL FLAGS");
    }

    [Fact]
    public async Task RunL4Therapist_ReceivesRawMemos_ParsesResponse()
    {
        var fake = new FakeOllamaAdapter("R|C=0.88\nI hear that sleep has become a struggle. What keeps you up at night?");
        var service = MakeService(fake);

        LayerResult result = await service.RunL4TherapistAsync(
            "sess_test", "I cannot sleep",
            analystMemoWire: "M|L=2|em=anxiety|sv=moderate|ri=insomnia|cp=worry",
            supervisorMemoWire: "M|L=3|ap=reflective_listening|tk=open_question|kq=What keeps you up?|rn=none",
            "INIT", Array.Empty<ChatMessage>());

        result.Ok.Should().BeTrue();
        result.Text.Should().Contain("sleep");
        result.Text.Should().Contain("struggle");
    }

    [Fact]
    public async Task RunL4Therapist_LlmError_ReturnsFailure()
    {
        var fake = new FakeOllamaAdapter(new LlmResponse { Ok = false, Error = "timeout" });
        var service = MakeService(fake);

        LayerResult result = await service.RunL4TherapistAsync(
            "sess_test", "hello",
            "M|L=2|em=low|sv=low",
            "M|L=3|ap=reflective|tk=open|kq=How are you?|rn=none",
            "INIT", Array.Empty<ChatMessage>());

        result.Ok.Should().BeFalse();
        result.Error.Should().Be("timeout");
    }
}
