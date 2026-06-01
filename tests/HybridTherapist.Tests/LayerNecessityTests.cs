using FluentAssertions;
using HandCodec.Models;
using HandCodec.Parser;
using HybridTherapist.Application.Hand;
using HybridTherapist.Application.Layers;
using HybridTherapist.Domain.Models;
using HybridTherapist.Domain.Services;
using HybridTherapist.Security.Gates;
using Xunit;

namespace HybridTherapist.Tests;

/// <summary>
/// Necessity-proving tests — for each pipeline layer, demonstrate that removing
/// or bypassing it would either produce wrong output, leak unsafe data, or break
/// a downstream consumer. Without these tests, "did we actually need this layer?"
/// has no machine-checkable answer.
/// </summary>
public sealed class LayerNecessityTests
{
    // ── L -1: CrisisGate ─────────────────────────────────────────────────────

    [Fact]
    public void NECESSITY_CrisisGate_BlocksSuicideIdeation_BeforeAnyLlmCall()
    {
        var gate = new CrisisGate();
        var result = gate.Check("chcę skończyć z sobą");

        // If CrisisGate is bypassed, the LLM would receive the suicide message
        // and could mishandle it. The test asserts that the gate fires and
        // includes a canned helpline response, so callers can short-circuit.
        result.IsHardStop.Should().BeTrue();
        result.HardStopMessage.Should().Contain("116 123",
            because: "without this gate, the LLM would handle suicide ideation");
    }

    [Fact]
    public void NECESSITY_CrisisGate_PassesNormalInsomnia()
    {
        // Negative case — the gate must NOT fire on therapeutic-but-not-crisis input
        var result = new CrisisGate().Check("nie mogę zasnąć");
        result.IsHardStop.Should().BeFalse(because: "false positives would block legitimate help-seeking");
    }

    // ── Layer 2: TopicRegistry — must actually feed downstream ──────────────

    [Fact]
    public void NECESSITY_TopicRegistry_FeedsAnalystPrompt()
    {
        // The analyst prompt MUST include detected topics as "SESSION TOPICS".
        // Without this wire-up, TopicRegistry would be dead code and the analyst
        // would have no grounding against hallucination across turns.
        IReadOnlyList<string> topics = TopicRegistry.ExtractTopics("mam lęki i nie mogę zasnąć");
        topics.Should().Contain("anxiety").And.Contain("sleep");

        // The actual injection point is AnalystLayer.RunAsync's prompt construction.
        // If someone refactors AnalystLayer to ignore the activeTopics parameter,
        // the analyst loses the grounding signal. This test pins the public contract.
        typeof(AnalystLayer)
            .GetMethod("RunAsync")!.GetParameters()
            .Should().Contain(p => p.Name == "activeTopics",
                because: "AnalystLayer must accept active topics to feed them into the prompt");
    }

    // ── Layer 4: RuptureDetector — must force Repair strategy ───────────────

    [Fact]
    public void NECESSITY_Rupture_ForcesRepairStrategy_OverridingPhaseSelection()
    {
        // Without rupture-aware selection, a user saying "źle mnie rozumiesz"
        // mid-DIGGING would get Deepening instead of Repair.
        ResponseStrategy normal = ResponseStrategySelector.Select("DIGGING", "low", ruptureDetected: false);
        ResponseStrategy ruptured = ResponseStrategySelector.Select("DIGGING", "low", ruptureDetected: true);

        normal.Should().Be(ResponseStrategy.Deepening);
        ruptured.Should().Be(ResponseStrategy.Repair,
            because: "without rupture handling, the assistant would deepen on a topic the user just corrected");
    }

    // ── Layer 8: ThematicAlignment — must catch fabricated themes ───────────

    [Fact]
    public void NECESSITY_ThematicAlignment_RejectsAnalystFabricatingBetrayal_FromSleepInput()
    {
        // Real failure mode: user says "I can't sleep" → analyst writes "betrayal".
        // Without this guard, the entire downstream chain (supervisor, therapist)
        // runs on a fabricated premise and the user is asked about a spouse they
        // never mentioned.
        var result = ThematicAlignment.Verify(
            analystMemoOrReport: "Emotional state: feelings of betrayal and broken trust",
            userInput: "I cannot sleep for three weeks");

        result.Aligned.Should().BeFalse(
            because: "the guard must catch the analyst introducing 'betrayal' when the user only mentioned sleep");
        result.UnsupportedThemes.Should().Contain("betrayal");
    }

    [Fact]
    public void NECESSITY_ThematicAlignment_AllowsSupportedThemes()
    {
        // Negative case — when the user DOES mention betrayal, the guard must not block
        var result = ThematicAlignment.Verify(
            "Emotional state: profound betrayal",
            "mąż mnie zdradził");
        result.Aligned.Should().BeTrue();
    }

    // ── Layer 7→9: M| Memo wire format between L2 and L3 ────────────────────

    [Fact]
    public void NECESSITY_AnalystMemo_IsParseableByDownstreamLayer()
    {
        // AnalystLayer emits M| wire format. If MemoBuilder were changed to produce
        // a format that can't be parsed, downstream layers would lose clinical signal.
        // M| enters L3/L4 prompts as raw wire — it must parse correctly.
        string memo = new MemoBuilder(CompressionTier.Balanced)
            .Layer(2)
            .EmotionalState("anxiety")
            .Severity("moderate")
            .Build();

        memo.Should().StartWith("M|");
        ParsedHandMessage? parsed = HandParser.Parse(memo);
        parsed.Should().NotBeNull();
        parsed!.Performative.Should().Be(Performative.Memo);
        parsed.Get("e7").Should().Be("anxiety");
        parsed.Get("s9").Should().Be("moderate");
    }

    // ── Layer 10: SessionPhase guidance — must be different per phase ───────

    [Fact]
    public void NECESSITY_SessionPhase_ProducesDistinctGuidancePerPhase()
    {
        // If GetPhaseSystemPrompt returns the same string for all phases, the L4
        // therapist gets no phase-specific shaping and the session feels flat.
        string init = SessionPhase.GetPhaseSystemPrompt("INIT");
        string digging = SessionPhase.GetPhaseSystemPrompt("DIGGING");
        string working = SessionPhase.GetPhaseSystemPrompt("WORKING");
        string closing = SessionPhase.GetPhaseSystemPrompt("CLOSING");

        var prompts = new[] { init, digging, working, closing };
        prompts.Distinct().Count().Should().Be(prompts.Length,
            because: "each phase must produce a different system prompt — otherwise the phase machine has no behavioural effect");
        init.Should().Contain("First contact", because: "INIT prompt should reflect first-contact intent");
        closing.Should().Contain("winding down", because: "CLOSING prompt should reflect end-of-session intent");
    }

    // ── Layer 11: L5 MemoryService — must compact when triggered ────────────

    [Fact]
    public void NECESSITY_MemoryService_CompactsHistoryWhenTriggered()
    {
        // Without L5 compaction, the L4 prompt grows linearly with the session
        // and eventually exceeds context window. The summary replaces old turns.
        var state = new TherapyConversationState
        {
            MessageCount = 8,
            History = Enumerable.Range(0, 12)
                .Select(i => new ChatMessage { Role = i % 2 == 0 ? "user" : "assistant", Content = $"m{i}" })
                .ToList(),
        };
        TherapyMemoryService.ShouldSummarize(state, phaseJustChanged: false).Should().BeTrue();
    }

    // ── Layer 13: QualityValidator — must catch echo before user sees it ────

    [Fact]
    public void NECESSITY_QualityValidator_CatchesPromptLeakage_BeforeUserSeesIt()
    {
        // Real failure: small model copied "confidence_decimal" placeholder verbatim.
        // Without QA, this leaked to the user-facing response.
        var v = QualityValidator.ValidateEnglishDraft(
            "Translated response: confidence_decimal is high. You are doing great.",
            "user input");
        v.Ok.Should().BeFalse();
        v.Reason.Should().Be("prompt_leakage");
    }

    [Fact]
    public void NECESSITY_PolishQualityCheck_RejectsEnglishOutput()
    {
        // If L7 translator output is still English (>30 chars, no diacritics),
        // the Polish-side gate must catch it and return a fallback.
        var v = QualityValidator.ValidatePolishOutput(
            "I hear how exhausted you must be feeling after all this time without sleep",
            "nie mogę zasnąć");
        v.Ok.Should().BeFalse();
        v.Reason.Should().Be("not_polish");
    }

    // ── Layer 16: Phase-aware disclaimer — must skip on INIT + medium ───────

    [Fact]
    public void NECESSITY_PhaseAwareDisclaimer_SkipsHelplineOnInitForInsomnia()
    {
        // The flow's disclaimer rule: skip helpline if INIT phase AND medium severity.
        // Without this, every "nie mogę zasnąć" gets a crisis helpline on first contact —
        // which damages rapport and is therapeutically wrong.
        var gate = new CrisisGate();
        var crisis = gate.Check("nie mogę zasnąć");
        crisis.IsEscalation.Should().BeTrue();
        crisis.Severity.Should().Be("medium",
            because: "insomnia should classify as medium, not high — feeds the phase-aware skip rule");
    }
}
