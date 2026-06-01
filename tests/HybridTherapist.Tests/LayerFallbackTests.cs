using FluentAssertions;
using HandCodec.Models;
using HandCodec.Parser;
using HybridTherapist.Application.Hand;
using HybridTherapist.Application.Layers;
using HybridTherapist.Application.Options;
using HybridTherapist.Domain.Models;
using HybridTherapist.Infrastructure.Adapters;
using HybridTherapist.Tests.Fakes;
using Microsoft.Extensions.Options;
using Xunit;

namespace HybridTherapist.Tests;

/// <summary>
/// Tests that AnalystLayer and SupervisorLayer gracefully degrade to safe fallback memos
/// when the LLM returns garbage (Level 5) or fails entirely (error path).
/// </summary>
public sealed class LayerFallbackTests
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

    private static AnalystLayer MakeAnalyst(IOllamaAdapter ollama) =>
        new(ollama, Microsoft.Extensions.Options.Options.Create(_opts), new NullTraceSink());

    private static SupervisorLayer MakeSupervisor(IOllamaAdapter ollama) =>
        new(ollama, Microsoft.Extensions.Options.Options.Create(_opts), new NullTraceSink());

    // ── AnalystLayer ───────────────────────────────────────────────────────

    [Fact]
    public async Task Analyst_LlmReturnsGarbage_EmitsLevel5FallbackMemo()
    {
        var fake = new FakeOllamaAdapter("asdfghjkl totalny belkot bez formatu");
        var analyst = MakeAnalyst(fake);

        AnalystResult result = await analyst.RunAsync("sess_test", "I cannot sleep",
            Array.Empty<ChatMessage>(), Array.Empty<string>());

        result.Ok.Should().BeTrue();
        result.Memo.Should().Contain("e7=unknown");
        result.Memo.Should().Contain("s9=low");
        result.Memo.Should().Contain("decoder_level5_fallback");
        result.Memo.Should().StartWith("M|L=2|");

        ParsedHandMessage? parsed = HandParser.Parse(result.Memo);
        parsed.Should().NotBeNull();
        parsed!.Performative.Should().Be(Performative.Memo);
    }

    [Fact]
    public async Task Analyst_LlmError_EmitsErrorFallbackMemo()
    {
        var fake = new FakeOllamaAdapter(new LlmResponse { Ok = false, Error = "connection refused" });
        var analyst = MakeAnalyst(fake);

        AnalystResult result = await analyst.RunAsync("sess_test", "I cannot sleep",
            Array.Empty<ChatMessage>(), Array.Empty<string>());

        result.Ok.Should().BeFalse();
        result.Memo.Should().Contain("e7=unknown");
        result.Memo.Should().Contain("s9=low");
        result.Memo.Should().Contain("note=llm_error");
        result.Memo.Should().StartWith("M|L=2|");
        result.Error.Should().Be("connection refused");
    }

    [Fact]
    public async Task Analyst_LlmReturnsValidMWire_ParsesCorrectly()
    {
        var fake = new FakeOllamaAdapter("M|L=2|e7=anxiety|s9=moderate|x4=insomnia|y1=worry|q3=sleep_quote");
        var analyst = MakeAnalyst(fake);

        AnalystResult result = await analyst.RunAsync("sess_test", "I cannot sleep",
            Array.Empty<ChatMessage>(), Array.Empty<string>());

        result.Ok.Should().BeTrue();
        result.Memo.Should().Contain("e7=anxiety");
        result.Memo.Should().Contain("s9=moderate");
        result.Memo.Should().NotContain("decoder_level5");
    }

    // ── SupervisorLayer ────────────────────────────────────────────────────

    [Fact]
    public async Task Supervisor_LlmReturnsGarbage_EmitsLevel5FallbackMemo()
    {
        var fake = new FakeOllamaAdapter("totalny belkot bez zadnego formatu");
        var supervisor = MakeSupervisor(fake);

        SupervisorResult result = await supervisor.RunAsync("sess_test", "I cannot sleep",
            "M|L=2|e7=anxiety|s9=moderate", ResponseStrategy.Intake);

        result.Ok.Should().BeTrue();
        result.Memo.Should().Contain("p3=behavioral_activation");
        result.Memo.Should().Contain("t5=schedule_one_small_activity");
        result.Memo.Should().Contain("decoder_level5_fallback");
        result.Memo.Should().StartWith("M|L=3|");
        result.Approach.Should().Be("behavioral_activation");

        ParsedHandMessage? parsed = HandParser.Parse(result.Memo);
        parsed.Should().NotBeNull();
        parsed!.Performative.Should().Be(Performative.Memo);
    }

    [Fact]
    public async Task Supervisor_LlmError_EmitsErrorFallbackMemo()
    {
        var fake = new FakeOllamaAdapter(new LlmResponse { Ok = false, Error = "timeout" });
        var supervisor = MakeSupervisor(fake);

        SupervisorResult result = await supervisor.RunAsync("sess_test", "I cannot sleep",
            "M|L=2|e7=anxiety|s9=moderate", ResponseStrategy.Intake);

        result.Ok.Should().BeFalse();
        result.Memo.Should().Contain("p3=behavioral_activation");
        result.Memo.Should().Contain("t5=schedule_one_small_activity");
        result.Memo.Should().Contain("note=llm_error");
        result.Memo.Should().StartWith("M|L=3|");
        result.Error.Should().Be("timeout");
    }

    [Fact]
    public async Task Supervisor_LlmReturnsValidMWire_ParsesCorrectly()
    {
        var fake = new FakeOllamaAdapter("M|L=3|p3=CBT|t5=cognitive_restructuring|k2=What_evidence_supports_that?|r8=none");
        var supervisor = MakeSupervisor(fake);

        SupervisorResult result = await supervisor.RunAsync("sess_test", "I cannot sleep",
            "M|L=2|e7=anxiety|s9=moderate", ResponseStrategy.Intake);

        result.Ok.Should().BeTrue();
        result.Memo.Should().Contain("p3=CBT");
        result.Memo.Should().Contain("t5=cognitive_restructuring");
        result.Approach.Should().Be("CBT");
        result.Memo.Should().NotContain("decoder_level5");
    }

    [Fact]
    public void Supervisor_ExtractApproach_FromValidMemo()
    {
        string approach = SupervisorLayer.ExtractApproach("M|L=3|p3=CBT|t5=reframing|k2=test|r8=none");
        approach.Should().Be("CBT");
    }

    [Fact]
    public void Supervisor_ExtractApproach_EmptyInput_ReturnsUnknown()
    {
        SupervisorLayer.ExtractApproach("").Should().Be("unknown");
        SupervisorLayer.ExtractApproach("not a memo").Should().Be("unknown");
    }
}
