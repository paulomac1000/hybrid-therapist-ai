using System.Security.Cryptography;
using System.Text;
using HandCodec.Models;
using HandCodec.Parser;
using HybridTherapist.Application.Hand;
using HybridTherapist.Application.Layers;
using HybridTherapist.Application.Options;
using HybridTherapist.Domain.Interfaces;
using HybridTherapist.Domain.Models;
using HybridTherapist.Domain.Services;
using HybridTherapist.Security.Gates;
using HybridTherapist.Security.Privacy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HybridTherapist.Application.Flows;

/// <summary>
/// Socrates multi-agent therapy pipeline — local-only (zero cloud), full cortexa parity.
///
/// Layer order (security first, always):
/// -1. CrisisGate           — hard-stop on crisis input (blocking, &lt;1s, never reaches LLM)
///  0. PrivacySanitizer     — PII redaction before any LLM call
///  1. StateLoader          — InMemoryTherapyStateRepository
///  2. TopicExtraction      — TopicRegistry → state.Topics → fed into L2 Analyst
///  3. PhaseMachine         — INIT → EXPLORATION → DIGGING → WORKING → CLOSING
///  4. RuptureDetector      — detect user correction → force Repair strategy
///  5. ResponseStrategy     — phase × severity × rupture → 1 of 10 strategies
///  6. L1 PL→EN             — Bielik 7B local
///  7. L2 Analyst           — MentaLLaMA reads topics+history → M|L=2 Memo
///  8. ThematicAlignment    — null memo if analyst fabricated sensitive themes
///  9. L3 Supervisor        — PsyLLM reads L2 Memo → M|L=3 Memo
/// 10. L5 MemoryService     — if msgs % 8 OR phase change → summary + truncate history
/// 11. L4 Therapist         — PsychoCounsel reads both memos + summary + phase guidance → EN draft
/// 12. L6 Calibrator        — Llama4-Dolphin polishes draft
/// 13. QualityValidator     — Stage-2 QA: echo, length, prompt leakage
/// 14. L7 EN→PL             — Bielik 7B local with quality gate
/// 15. PolishQualityCheck   — final language + echo check
/// 16. Disclaimer           — phase-aware (INIT + medium severity → skip)
/// 17. Audit                — InMemoryTraceSink + structured Serilog
/// </summary>
public sealed class TherapistFlow : ITherapistFlow
{
    private readonly CrisisGate _crisisGate;
    private readonly PrivacySanitizer _privacySanitizer;
    private readonly ITherapyConversationStateRepository _stateRepo;
    private readonly TherapistLayerService _layers;
    private readonly AnalystLayer _analyst;
    private readonly SupervisorLayer _supervisor;
    private readonly TherapyMemoryService _memory;
    private readonly ILogger<TherapistFlow> _logger;
    private readonly TokenSavingsTracker _tokenTracker = new();
    private readonly CompressionTier _compressionTier;

    public TherapistFlow(
        CrisisGate crisisGate,
        PrivacySanitizer privacySanitizer,
        ITherapyConversationStateRepository stateRepo,
        TherapistLayerService layers,
        AnalystLayer analyst,
        SupervisorLayer supervisor,
        TherapyMemoryService memory,
        IOptions<TherapistOptions> opts,
        ILogger<TherapistFlow>? logger = null)
    {
        _crisisGate = crisisGate;
        _privacySanitizer = privacySanitizer;
        _stateRepo = stateRepo;
        _layers = layers;
        _analyst = analyst;
        _supervisor = supervisor;
        _memory = memory;
        _compressionTier = opts.Value.HandCompressionTier;
        _logger = logger ?? NullLogger<TherapistFlow>.Instance;
    }

    public async Task<FlowExecutionResult> ExecuteAsync(ChatCompletionRequest request, CancellationToken ct = default)
    {
        string userText = request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty;
        string sessionId = ResolveSessionId(request);

        // ── Layer -1: CrisisGate ───────────────────────────────────────────────
        CrisisGateResult crisis = _crisisGate.Check(userText);
        if (crisis.IsHardStop)
        {
            _logger.LogWarning("Crisis hard-stop for {Session}", sessionId);
            IReadOnlyList<string> crisisTopics = TopicRegistry.ExtractTopics(userText);
            return new FlowExecutionResult
            {
                Model = request.Model,
                Content = crisis.HardStopMessage ?? "Crisis detected.",
                CrisisDetected = true,
                Metadata =
                {
                    ["crisis_detected"] = true,
                    ["crisis_severity"] = "critical",
                    ["session_id"] = sessionId,
                    ["trace_url"] = $"/v1/trace/{sessionId}",
                    ["strategy"] = "HardStop",
                    ["severity"] = "critical",
                    ["phase"] = "INIT",
                    ["topics"] = crisisTopics.ToArray(),
                    ["thematic_alignment"] = true,
                    ["rupture_detected"] = false,
                    ["fallback"] = false,
                },
            };
        }

        // ── Layer 0: PrivacySanitizer ─────────────────────────────────────────
        string sanitized = _privacySanitizer.Sanitize(userText, "therapeutic");

        // ── Layer 1: StateLoader ──────────────────────────────────────────────
        TherapyConversationState state = await _stateRepo.GetAsync(sessionId, ct);

        // ── Layer 2: TopicExtraction (used by L2 Analyst prompt) ──────────────
        IReadOnlyList<string> freshTopics = TopicRegistry.ExtractTopics(sanitized);
        state.Topics = TopicRegistry.Merge(state.Topics, freshTopics).ToList();

        // ── Layer 3: PhaseMachine ─────────────────────────────────────────────
        string previousPhase = state.CurrentPhase;
        state.MessageCount++;
        state.MessagesInPhase++;
        string severity = crisis.IsEscalation ? crisis.Severity : "low";
        string newPhase = SessionPhase.Evaluate(state.CurrentPhase, state.MessageCount, severity);
        bool phaseChanged = newPhase != previousPhase;
        if (phaseChanged)
        {
            _logger.LogInformation("Phase transition {From} → {To} for {Session}", previousPhase, newPhase, sessionId);
            state.CurrentPhase = newPhase;
            state.MessagesInPhase = 1;
        }

        // ── Layer 4: RuptureDetector ──────────────────────────────────────────
        string? lastAssistantMessage = state.History.LastOrDefault(m => m.Role == "assistant")?.Content;
        RuptureDetector.Result rupture = RuptureDetector.Check(sanitized, lastAssistantMessage);

        // ── Layer 5: ResponseStrategy ─────────────────────────────────────────
        ResponseStrategy strategy = ResponseStrategySelector.Select(state.CurrentPhase, severity, rupture.Detected);

        // ── Layer 6: L1 PL → EN ───────────────────────────────────────────────
        LayerResult l1 = await _layers.RunL1TranslatePlToEnAsync(sessionId, sanitized, ct);
        if (!l1.Ok)
        {
            _logger.LogWarning("L1 PL→EN failed for {Session}: {Error} — continuing with raw Polish text", sessionId, l1.Error);
        }
        string userTextEn = l1.Ok ? l1.Text : sanitized;

        // ── Layer 7: L2 Analyst (active topics + history → M|L=2 Memo) ────────
        AnalystResult analyst = await _analyst.RunAsync(sessionId, userTextEn, state.History, state.Topics, ct);
        string analystMemoWire = analyst.Memo;
        _tokenTracker.Record(TokenSavingsTracker.ExpandMemoToPlaintext(analystMemoWire), analystMemoWire);

        // ── Layer 8: ThematicAlignment (anti-hallucination) ───────────────────
        ThematicAlignment.Result alignment = ThematicAlignment.Verify(
            analystMemoWire, sanitized);
        if (!alignment.Aligned)
        {
            _logger.LogWarning("Analyst fabricated themes for {Session}: {Themes} — redacting memo",
                sessionId, string.Join(",", alignment.UnsupportedThemes));
            analystMemoWire = new MemoBuilder(_compressionTier)
                .Layer(2)
                .EmotionalState("unknown")
                .Severity("low")
                .Field("note", "memo_redacted_thematic_misalignment")
                .Build();
        }

        // ── Layer 9: L3 Supervisor (reads L2 Memo → M|L=3 Memo) ───────────────
        SupervisorResult supervisor = await _supervisor.RunAsync(sessionId, userTextEn, analystMemoWire, strategy, ct);
        string supervisorMemoWire = supervisor.Memo;
        _tokenTracker.Record(TokenSavingsTracker.ExpandMemoToPlaintext(supervisorMemoWire), supervisorMemoWire);

        // ── Layer 10: L5 MemoryService — summarize BEFORE L4 so summary is usable ──
        if (TherapyMemoryService.ShouldSummarize(state, phaseChanged))
        {
            var tier = TherapyMemoryService.GetCompactionTier(state.MessageCount, phaseChanged);
            await _memory.SummarizeAndCompactAsync(sessionId, state, tier, ct);
        }

        // ── Layer 11: L4 Therapist (memos as raw M| + summary + phase guidance) ──
        LayerResult l4 = await _layers.RunL4TherapistAsync(
            sessionId, userTextEn, analystMemoWire, supervisorMemoWire,
            state.CurrentPhase, state.History,
            structuredSummary: state.StructuredSummary,
            ct: ct);

        if (!l4.Ok)
        {
            _logger.LogError("L4 Therapist failed for {Session}: {Error}", sessionId, l4.Error);
            return BuildFallback(request.Model, sessionId, "L4_therapist", l4.Error ?? "unknown");
        }

        // ── Layer 12: L6 Calibrator ───────────────────────────────────────────
        LayerResult l6 = await _layers.RunL6CalibratorAsync(
            sessionId, l4.Text, supervisorMemoWire, userTextEn,
            currentPhase: state.CurrentPhase,
            recentHistory: state.History,
            ct: ct);
        string enResponse = l6.Ok ? l6.Text : l4.Text;

        // ── Layer 13: QualityValidator (Stage-2 QA on EN draft) ───────────────
        QualityValidator.Verdict qa1 = QualityValidator.ValidateEnglishDraft(enResponse, userTextEn);
        if (!qa1.Ok)
        {
            _logger.LogWarning("EN QA failed for {Session}: {Reason}", sessionId, qa1.Reason);
            enResponse = l4.Text;
        }

        QualityValidator.Verdict tq = QualityValidator.ValidateTherapeuticQuality(
            enResponse, state.CurrentPhase, state.MessageCount);
        if (!tq.Ok)
        {
            _logger.LogWarning("Therapeutic quality check failed for {Session}: {Reason} — blocking response", sessionId, tq.Reason);
            return BuildFallback(request.Model, sessionId, "L6_therapeutic_quality", tq.Reason);
        }

        // ── Layer 14: L7 EN → PL ──────────────────────────────────────────────
        LayerResult l7 = await _layers.RunL7TranslateEnToPlAsync(sessionId, enResponse, sanitized, ct);
        bool fallback = !l7.Ok;
        string plResponse = l7.Text;

        // ── Layer 15: PolishQualityCheck ──────────────────────────────────────
        QualityValidator.Verdict qa2 = QualityValidator.ValidatePolishOutput(plResponse, userText);
        if (!qa2.Ok)
        {
            _logger.LogWarning("PL QA failed for {Session}: {Reason}", sessionId, qa2.Reason);
            plResponse = "Przepraszam, mam chwilowe trudności z odpowiedzią. Możesz spróbować raz jeszcze?";
            fallback = true;
        }

        // ── Layer 16: Disclaimer (phase-aware) ────────────────────────────────
        if (crisis.IsEscalation && crisis.Severity != "medium" && state.CurrentPhase != "INIT")
        {
            plResponse += "\n\nJeśli czujesz się w kryzysie, skontaktuj się z Telefonem Zaufania: 116 123.";
        }

        // ── Layer 17: Audit ───────────────────────────────────────────────────
        _logger.LogInformation(
            "Session {Session} | Phase {Phase} | Strategy {Strategy} | Rupture {Rupture} | Msg {Count} | Fallback {Fallback}",
            sessionId, state.CurrentPhase, strategy, rupture.Detected, state.MessageCount, fallback);

        TokenSavingsSummary savings = _tokenTracker.Summary();
        _logger.LogInformation(
            "Token savings for {Session}: wire {WireT} vs plaintext {PlainT} = {Saved} tokens saved ({Pct}%)",
            sessionId, savings.WireTokensEstimate, savings.PlaintextTokensEstimate,
            savings.TokensSaved, savings.SavingsPercent);

        // ── Save state ────────────────────────────────────────────────────────
        state.History.Add(new ChatMessage { Role = "user", Content = userText });
        state.History.Add(new ChatMessage { Role = "assistant", Content = plResponse });
        if (state.History.Count > 40)
            state.History = state.History.TakeLast(40).ToList();
        await _stateRepo.SaveAsync(state, ct);

        return new FlowExecutionResult
        {
            Model = request.Model,
            Content = plResponse,
            Fallback = fallback,
            CrisisDetected = l7.HasCrisisSignal,
            Metadata =
            {
                ["phase"] = state.CurrentPhase,
                ["strategy"] = strategy.ToString(),
                ["severity"] = severity,
                ["message_count"] = state.MessageCount,
                ["topics"] = state.Topics.ToArray(),
                ["rupture_detected"] = rupture.Detected,
                ["rupture_reason"] = rupture.Reason ?? string.Empty,
                ["thematic_alignment"] = alignment.Aligned,
                ["analyst_severity"] = analyst.Report?.Severity.ToString().ToLowerInvariant() ?? "unknown",
                ["supervisor_approach"] = supervisor.Approach,
                ["crisis_detected"] = l7.HasCrisisSignal,
                ["fallback"] = fallback,
                ["session_id"] = sessionId,
                ["trace_url"] = $"/v1/trace/{sessionId}",
                ["token_savings_tokens"] = savings.TokensSaved,
                ["token_savings_percent"] = savings.SavingsPercent,
            },
        };
    }

    private static FlowExecutionResult BuildFallback(string model, string sessionId, string failedLayer, string? errorReason) => new()
    {
        Model = model,
        Content = "Przepraszam, mam chwilowe trudności techniczne. Spróbuj ponownie za chwilę.",
        Fallback = true,
        Metadata =
        {
            ["fallback"] = true,
            ["session_id"] = sessionId,
            ["trace_url"] = $"/v1/trace/{sessionId}",
            ["failed_layer"] = failedLayer,
            ["error_reason"] = errorReason ?? "unknown",
        },
    };

    private static string ResolveSessionId(ChatCompletionRequest request)
    {
        string firstUser = request.Messages.FirstOrDefault(m => m.Role == "user")?.Content ?? string.Empty;
        if (firstUser.Length == 0) return Guid.NewGuid().ToString();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(firstUser));
        return $"sess_{Convert.ToHexString(hash)[..8].ToLowerInvariant()}";
    }
}
