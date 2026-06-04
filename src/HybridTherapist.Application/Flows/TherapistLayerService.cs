using System.Diagnostics;
using System.Text;
using HandCodec.Models;
using HybridTherapist.Application.Hand;
using HybridTherapist.Application.Options;
using HybridTherapist.Domain.Interfaces;
using HybridTherapist.Domain.Models;
using HybridTherapist.Domain.Services;
using HybridTherapist.Infrastructure.Adapters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HybridTherapist.Application.Flows;

/// <summary>
/// Orchestrates the L1-L7 Socrates pipeline layers — ALL LOCAL (Ollama only, zero cloud).
/// Every layer call is recorded in <see cref="ITraceSink"/> for debugging via
/// <c>GET /v1/trace/{sessionId}</c>.
///
/// Translator strategy: L1 has 2-pass retry (2s delay on first failure),
/// L7 is single-pass with quality gate. If output is still English → static Polish fallback.
/// </summary>
public sealed class TherapistLayerService
{
    private readonly IOllamaAdapter _ollama;
    private readonly TherapistOptions _opts;
    private readonly ITraceSink _trace;
    private readonly ILogger<TherapistLayerService> _logger;

    public TherapistLayerService(
        IOllamaAdapter ollama,
        IOptions<TherapistOptions> opts,
        ITraceSink trace,
        ILogger<TherapistLayerService>? logger = null)
    {
        _ollama = ollama;
        _opts = opts.Value;
        _trace = trace;
        _logger = logger ?? NullLogger<TherapistLayerService>.Instance;
    }

    // ── L1 — Translator PL → EN (local-only) ─────────────────────────────────

    private async Task<LayerResult> RunL1TranslateOnceAsync(
        string sessionId, string userTextPl, IReadOnlyList<HandTurn> messages, string errorOutcome, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();
        LlmResponse resp = await _ollama.GenerateChatAsync(messages, 300, 0.1f, _opts.Translator, ct);
        sw.Stop();

        if (!resp.Ok)
        {
            await TraceAsync(sessionId, "L1_pl_en", _opts.Translator, userTextPl, string.Empty, sw.ElapsedMilliseconds, errorOutcome, resp.Error);
            return new LayerResult { Ok = false, Error = resp.Error };
        }

        string text = DecodeHand(resp.Text);
        await TraceAsync(sessionId, "L1_pl_en", resp.ModelId ?? _opts.Translator, userTextPl, text, sw.ElapsedMilliseconds, "ok", wireFormat: resp.Text);
        return new LayerResult { Ok = true, Text = text, ModelId = resp.ModelId };
    }

    public async Task<LayerResult> RunL1TranslatePlToEnAsync(string sessionId, string userTextPl, CancellationToken ct = default)
    {
        const string SystemPrompt =
            "You are a translator working in a mental health therapy context. " +
            "Translate the following Polish message from a therapy patient to natural, conversational English. " +
            "The user is Polish — interpret words in their psychological and everyday meaning, not literal dictionary translations. " +
            "\"Wakacje\" means vacation/time off, not Christmas holidays. " +
            "\"Urlop\" means time off work. " +
            "Output ONLY the English translation. " +
            "Do NOT explain. Do NOT continue the conversation. Do NOT echo the prompt.";

        IReadOnlyList<HandTurn> messages = HandConversationBuilder.Build(
            SystemPrompt, HandCheckpointLibrary.SystemPing, userTextPl,
            Performative.Result, _opts.AgentClass);

        // Pass 1: try once
        LayerResult first = await RunL1TranslateOnceAsync(sessionId, userTextPl, messages, "error", ct);
        if (first.Ok) return first;

        _logger.LogWarning("L1 Translator first attempt failed for {Session}: {Error} — retrying once", sessionId, first.Error);

        // Pass 2: brief pause for model-load transient, then retry
        await Task.Delay(2000, ct);
        LayerResult second = await RunL1TranslateOnceAsync(sessionId, userTextPl, messages, "retry_still_error", ct);
        if (second.Ok) return second;

        _logger.LogError("L1 Translator both attempts failed for {Session}: {Error}", sessionId, second.Error);
        return second;
    }

    // ── L4 — PsychoCounsel Therapist ─────────────────────────────────────────

#pragma warning disable S107 // Layer orchestration — pipeline context requires multiple params
    public async Task<LayerResult> RunL4TherapistAsync(
        string sessionId, string userTextEn, string analystMemoWire, string supervisorMemoWire,
        string currentPhase, IReadOnlyList<ChatMessage> history,
        MemorySummary? structuredSummary = null,
        int maxTokens = 400, CancellationToken ct = default)
    {
        string historyText = history.Count > 0
            ? string.Join("\n", history.TakeLast(6).Select(m => $"{m.Role}: {m.Content}"))
            : "No prior history.";

        string memoryBlock = BuildMemoryBlock(structuredSummary, currentPhase);

        string prompt =
            $"Phase: {currentPhase}\n" +
            memoryBlock +
            $"[ANALYST MEMO]\n{analystMemoWire}\n\n" +
            $"[SUPERVISOR MEMO]\n{supervisorMemoWire}\n\n" +
            $"[RECENT HISTORY]\n{historyText}\n\n" +
            $"User: {userTextEn}\n\n" +
            "Respond as an empathetic therapist. Keep under 200 words. " +
            "NEVER repeat or echo the user's words. Generate an original therapeutic response.";

        string systemPrompt = BuildL4SystemPrompt();

        IReadOnlyList<HandTurn> messages = HandConversationBuilder.Build(
            systemPrompt, HandCheckpointLibrary.SystemPing, prompt,
            Performative.Result, _opts.AgentClass);

        Stopwatch sw = Stopwatch.StartNew();
        LlmResponse resp = await _ollama.GenerateChatAsync(messages, maxTokens, 0.7f, _opts.Therapist, ct);
        sw.Stop();

        if (!resp.Ok)
        {
            await TraceAsync(sessionId, "L4_therapist", _opts.Therapist, prompt, string.Empty, sw.ElapsedMilliseconds, "error", resp.Error);
            return new LayerResult { Ok = false, Error = resp.Error };
        }

        string text = DecodeHand(resp.Text);
        await TraceAsync(sessionId, "L4_therapist", resp.ModelId ?? _opts.Therapist, prompt, text, sw.ElapsedMilliseconds, "ok", wireFormat: resp.Text);
        return new LayerResult { Ok = true, Text = text, ModelId = resp.ModelId };
    }

    // ── L6 — Llama4-Dolphin Calibrator ───────────────────────────────────────

    public async Task<LayerResult> RunL6CalibratorAsync(
        string sessionId, string therapistDraft, string supervisorGuidance, string userTextEn,
        string currentPhase,
        IReadOnlyList<ChatMessage>? recentHistory = null,
        CancellationToken ct = default)
    {
        string phaseGuidance = SessionPhase.GetCalibratorPhaseGuidance(currentPhase);

        string recentResponsesText = "";
        if (recentHistory is { Count: > 0 })
        {
            var recentAssistant = recentHistory
                .Where(m => m.Role == "assistant")
                .TakeLast(3)
                .ToList();
            if (recentAssistant.Count > 0)
            {
                recentResponsesText = "[RECENT RESPONSES — DO NOT REPEAT OPENINGS OR QUESTIONS]\n" +
                    string.Join("\n---\n", recentAssistant.Select(m => m.Content)) +
                    "\n\n";
            }
        }

        string prompt =
            $"[PHASE GUIDANCE — {currentPhase}]\n{phaseGuidance}\n\n" +
            recentResponsesText +
            $"[SUPERVISOR GUIDANCE]\n{supervisorGuidance}\n\n" +
            $"[THERAPIST DRAFT — source of truth]\n{therapistDraft}\n\n" +
            $"User said: {userTextEn}\n\n" +
            "You are a final editor. Keep all facts from the THERAPIST DRAFT. " +
            "Use supervisor guidance only for technique. " +
            "NEVER introduce new topics. NEVER open with formulaic phrases like 'I understand that' or 'It seems that'. " +
            "Vary the opening. End with one open-ended question. Respond in English only.";

        string systemPrompt =
            "You are a therapeutic response editor. Maintain the therapist's voice and content. " +
            "NEVER repeat openings from recent responses. Vary your opening style every turn.";

        Stopwatch sw = Stopwatch.StartNew();
        LlmResponse resp = await _ollama.GenerateAsync(prompt, systemPrompt, 500, 0.6f, _opts.Calibrator, ct);
        sw.Stop();

        if (!resp.Ok)
        {
            _logger.LogWarning("L6 Calibrator failed for {Session}, using therapist draft as-is", sessionId);
            await TraceAsync(sessionId, "L6_calibrator", _opts.Calibrator, prompt, therapistDraft, sw.ElapsedMilliseconds, "fallback_to_draft", resp.Error);
            return new LayerResult { Ok = true, Text = therapistDraft, ModelId = _opts.Calibrator };
        }

        string text = resp.Text.Trim();
        await TraceAsync(sessionId, "L6_calibrator", resp.ModelId ?? _opts.Calibrator, prompt, text, sw.ElapsedMilliseconds, "ok");
        return new LayerResult { Ok = true, Text = text, ModelId = resp.ModelId };
    }

    // ── L7 — Translator EN → PL (local-only with quality gate) ────────────────

    public async Task<LayerResult> RunL7TranslateEnToPlAsync(
        string sessionId, string englishText, string originalUserTextPl, CancellationToken ct = default)
    {
        const string SystemPrompt =
            "You are a Polish translator. Translate the English text the user sends to natural Polish. " +
            "The text is a therapist's reply — preserve warmth and empathy. " +
            "Use natural, conversational Polish (no slash-separated gender forms). " +
            "Output ONLY the Polish translation. " +
            "Do NOT repeat the English. Do NOT add commentary. Do NOT echo the prompt.";

        _ = originalUserTextPl;

        IReadOnlyList<HandTurn> messages = HandConversationBuilder.Build(
            SystemPrompt, HandCheckpointLibrary.SystemPing, englishText,
            Performative.Result, _opts.AgentClass);

        Stopwatch sw = Stopwatch.StartNew();
        LlmResponse resp = await _ollama.GenerateChatAsync(messages, 600, 0.2f, _opts.Translator, ct);
        sw.Stop();

        if (resp.Ok)
        {
            string text = DecodeHand(resp.Text);
            if (!LooksMostlyEnglish(text))
            {
                await TraceAsync(sessionId, "L7_en_pl", resp.ModelId ?? _opts.Translator,
                    englishText, text, sw.ElapsedMilliseconds, "ok", wireFormat: resp.Text);
        return new LayerResult { Ok = true, Text = text, ModelId = resp.ModelId };
    }
#pragma warning restore S107

            await TraceAsync(sessionId, "L7_en_pl", resp.ModelId ?? _opts.Translator,
                englishText, text, sw.ElapsedMilliseconds, "still_english");
        }
        else
        {
            await TraceAsync(sessionId, "L7_en_pl", _opts.Translator,
                englishText, string.Empty, sw.ElapsedMilliseconds, "error", resp.Error);
        }

        // Quality gate failed — static Polish fallback
        _logger.LogWarning("L7 EN->PL translation failed for {Session}, using static fallback", sessionId);
        return new LayerResult { Ok = false, Text = _opts.TranslationFallbackPl, Error = "translation_quality_gate_failed" };
    }

    internal static string BuildMemoryBlock(MemorySummary? ms, string phase)
    {
        if (ms is null) return string.Empty;

        var sb = new StringBuilder();

        sb.AppendLine("[SESSION OVERVIEW]");
        sb.AppendLine(ms.Overview);

        switch (phase)
        {
            case "INIT":
            case "CLOSING":
                break;

            case "EXPLORATION":
                if (ms.TopicMap.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("[DISCUSSED TOPICS]");
                    foreach (var t in ms.TopicMap)
                        sb.AppendLine($"- {t.Theme} ({t.Status})");
                }
                sb.AppendLine();
                sb.AppendLine($"[EMOTIONAL ARC] {ms.EmotionalArc}");
                break;

            case "DIGGING":
                if (ms.TopicMap.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("[TOPIC DETAIL]");
                    foreach (var t in ms.TopicMap)
                        sb.AppendLine($"- {t.Theme}: {t.Evolution} (msg {t.MessageRange})");
                }
                if (!string.IsNullOrWhiteSpace(ms.ClinicalFlags))
                {
                    sb.AppendLine();
                    sb.AppendLine("[CLINICAL FLAGS — PAY ATTENTION]");
                    sb.AppendLine(ms.ClinicalFlags);
                }
                if (!string.IsNullOrWhiteSpace(ms.FocusNext))
                {
                    sb.AppendLine();
                    sb.AppendLine("[SUGGESTED FOCUS]");
                    sb.AppendLine(ms.FocusNext);
                }
                break;

            case "WORKING":
                if (ms.TopicMap.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("[ACTIVE TOPICS]");
                    foreach (var t in ms.TopicMap.Where(t => t.Status == "active"))
                        sb.AppendLine($"- {t.Theme}");
                }
                if (!string.IsNullOrWhiteSpace(ms.FocusNext))
                {
                    sb.AppendLine();
                    sb.AppendLine("[SUGGESTED FOCUS]");
                    sb.AppendLine(ms.FocusNext);
                }
                break;

            default:
                sb.AppendLine();
                sb.AppendLine($"[EMOTIONAL ARC] {ms.EmotionalArc}");
                break;
        }

        return sb.ToString();
    }

    internal static string BuildL4SystemPrompt() =>
        "You are an empathetic therapist. Respond with warmth and clinical insight. " +
        "Do not give direct advice. Ask one open question to continue.";

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task TraceAsync(string sessionId, string layer, string? model, string input, string output,
        long durationMs, string outcome, string? error = null, string? wireFormat = null)
    {
        await _trace.RecordAsync(new TraceEvent(
            Timestamp: DateTimeOffset.UtcNow,
            SessionId: sessionId,
            Layer: layer,
            Model: model,
            Input: input.Length > 2000 ? input[..2000] + "...[truncated]" : input,
            Output: output.Length > 2000 ? output[..2000] + "...[truncated]" : output,
            DurationMs: durationMs,
            Outcome: outcome,
            Error: error,
            WireFormat: wireFormat));
    }

    private static bool LooksMostlyEnglish(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        int polishDiacritics = 0;
        int letters = 0;
        foreach (char c in text)
        {
            if (char.IsLetter(c)) letters++;
            if ("ąćęłńóśźżĄĆĘŁŃÓŚŹŻ".Contains(c, StringComparison.Ordinal)) polishDiacritics++;
        }
        if (letters < 30) return false;
        return polishDiacritics * 100 / letters < 2;
    }

    private string DecodeHand(string raw)
    {
        HandDecodedResponse decoded = HandResponseDecoder.Decode(raw, _opts.AgentClass);
        _logger.LogInformation(
            "Hand decode: resilience level {Level}, confidence {Confidence}",
            decoded.ResilienceLevel, decoded.Confidence);
        return decoded.Text;
    }
}
