using System.Diagnostics;
using System.Text;
using HybridTherapist.Application.Options;
using HybridTherapist.Domain.Interfaces;
using HybridTherapist.Domain.Models;
using HybridTherapist.Infrastructure.Adapters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HybridTherapist.Application.Layers;

public sealed class TherapyMemoryService
{
    private const int SummaryEveryNMessages = 8;
    private const int DeepSummaryEveryN = 24;
    private const int KeepLastNMessages = 6;

    private readonly IOllamaAdapter _ollama;
    private readonly TherapistOptions _opts;
    private readonly ITraceSink _trace;
    private readonly ILogger<TherapyMemoryService> _logger;

    public enum CompactionTier { Standard, Deep, Phase }

    public TherapyMemoryService(
        IOllamaAdapter ollama,
        IOptions<TherapistOptions> opts,
        ITraceSink trace,
        ILogger<TherapyMemoryService>? logger = null)
    {
        _ollama = ollama;
        _opts = opts.Value;
        _trace = trace;
        _logger = logger ?? NullLogger<TherapyMemoryService>.Instance;
    }

    public static CompactionTier GetCompactionTier(int messageCount, bool phaseJustChanged)
    {
        if (phaseJustChanged) return CompactionTier.Phase;
        if (messageCount > 0 && messageCount % DeepSummaryEveryN == 0) return CompactionTier.Deep;
        return CompactionTier.Standard;
    }

    public static bool ShouldSummarize(TherapyConversationState state, bool phaseJustChanged)
    {
        if (state.History.Count <= KeepLastNMessages) return false;
        if (phaseJustChanged) return true;
        return state.MessageCount > 0 && state.MessageCount % SummaryEveryNMessages == 0;
    }

    public async Task<MemorySummary?> SummarizeAndCompactAsync(
        string sessionId,
        TherapyConversationState state,
        CompactionTier tier,
        CancellationToken ct = default)
    {
        if (state.History.Count <= KeepLastNMessages) return state.StructuredSummary;

        string historyText = string.Join("\n", state.History.Select(m => $"{m.Role}: {m.Content}"));
        string previousSummary = state.StructuredSummary is not null
            ? FormatPreviousSummary(state.StructuredSummary)
            : "(no prior summary)";

        string systemPrompt = BuildSystemPrompt(tier);
        string userPrompt = BuildUserPrompt(tier, previousSummary, historyText);

        int maxTokens = tier is CompactionTier.Standard ? 350 : 600;

        Stopwatch sw = Stopwatch.StartNew();
        LlmResponse resp = await _ollama.GenerateAsync(userPrompt, systemPrompt, maxTokens, 0.3f, _opts.Calibrator, ct);
        sw.Stop();

        if (!resp.Ok || string.IsNullOrWhiteSpace(resp.Text))
        {
            await _trace.RecordAsync(new TraceEvent(
                DateTimeOffset.UtcNow, sessionId, "L5_memory", _opts.Calibrator,
                $"tier={tier},history_msgs={state.History.Count}", string.Empty, sw.ElapsedMilliseconds, "error", resp.Error), ct);
            _logger.LogWarning("L5 summarizer failed for {Session}: {Error}", sessionId, resp.Error);
            return state.StructuredSummary;
        }

        MemorySummary? parsed = MemorySummaryParser.Parse(resp.Text);
        if (parsed is null)
        {
            _logger.LogWarning("L5 parser could not parse output for {Session}, keeping current summary", sessionId);
            await _trace.RecordAsync(new TraceEvent(
                DateTimeOffset.UtcNow, sessionId, "L5_memory", _opts.Calibrator,
                $"tier={tier},history_msgs={state.History.Count}", resp.Text, sw.ElapsedMilliseconds, "parse_error",
                "MemorySummaryParser returned null"), ct);
            return state.StructuredSummary;
        }

        state.StructuredSummary = parsed with { EmotionalTrend = DetectTrend(parsed, state.StructuredSummary) };
        state.SessionSummary = parsed.Overview;
        state.History = state.History.TakeLast(KeepLastNMessages).ToList();

        await _trace.RecordAsync(new TraceEvent(
            DateTimeOffset.UtcNow, sessionId, "L5_memory", resp.ModelId ?? _opts.Calibrator,
            $"tier={tier},history_msgs_before=...", resp.Text, sw.ElapsedMilliseconds, "ok"), ct);

        _logger.LogInformation(
            "L5 {Tier} summary written for {Session}, history truncated to {N}",
            tier, sessionId, KeepLastNMessages);

        return parsed;
    }

    private static string DetectTrend(MemorySummary current, MemorySummary? previous)
    {
        if (previous is null) return "stable";

        string prevArc = (previous.EmotionalArc ?? "").ToLowerInvariant();
        string currArc = (current.EmotionalArc ?? "").ToLowerInvariant();

        bool prevHasCrisis = prevArc.Contains("crisis") || prevArc.Contains("high");
        bool currHasCrisis = currArc.Contains("crisis") || currArc.Contains("high");

        if (!prevHasCrisis && currHasCrisis) return "worsening";
        if (prevHasCrisis && !currHasCrisis) return "improving";
        return "stable";
    }

    private static string BuildSystemPrompt(CompactionTier tier) => tier switch
    {
        CompactionTier.Phase or CompactionTier.Deep =>
            "You are a clinical supervisor reviewing session progress. " +
            "Identify patterns, contradictions, stuck points, dropped themes, and risk. " +
            "Stay strictly grounded in the provided conversation. No invented themes. " +
            "Output ONLY the structured format — no preamble, no commentary.",

        CompactionTier.Standard =>
            "You are a clinical note-taker producing concise structured session summaries. " +
            "Stay strictly grounded in the provided conversation. No invented themes. " +
            "Output ONLY the structured format — no preamble, no commentary.",

        _ => "You are a clinical note-taker.",
    };

    private static string BuildUserPrompt(CompactionTier tier, string previousSummary, string historyText)
    {
        string sections = tier switch
        {
            CompactionTier.Standard =>
                "[OVERVIEW]\n2-3 sentence summary.\n\n" +
                "[TOPIC MAP]\nOne line per theme: theme: msg_range | evolution: ... | status: active/dormant/resolved\n\n" +
                "[EMOTIONAL ARC]\nemotional_states with (severity, msg_range) separated by →",

            CompactionTier.Deep or CompactionTier.Phase =>
                "[OVERVIEW]\n2-3 sentence summary.\n\n" +
                "[TOPIC MAP]\nOne line per theme: theme: msg_range | evolution: ... | status: active/dormant/resolved\n\n" +
                "[EMOTIONAL ARC]\nemotional_states with (severity, msg_range) separated by →\n\n" +
                "[CLINICAL FLAGS]\nCONTRADICTION: ...\nSTUCK: ...\nDROPPED: ...\nRISK: ...\n(Use 'none' for any flag with nothing to report)\n\n" +
                "[FOCUS NEXT]\nConcrete suggestions for the therapist. Use 'none' if nothing specific.",

            _ => "[OVERVIEW]\nSummary.\n\n[TOPIC MAP]\nTopics.\n\n[EMOTIONAL ARC]\nstable",
        };

        return $"PRIOR STRUCTURED SUMMARY:\n{previousSummary}\n\n" +
               $"FULL CONVERSATION SO FAR:\n{historyText}\n\n" +
               "Write an updated session summary in the following structured format:\n\n" +
               sections + "\n\n" +
               "Rules:\n" +
               "- [OVERVIEW] is always required\n" +
               "- [TOPIC MAP] one line per distinct theme, max 8 themes\n" +
               "- [EMOTIONAL ARC] is always required (minimum: 'stable')\n" +
               "- Section labels MUST use square brackets EXACTLY as shown\n" +
               "- Do not merge sections. Keep one blank line between sections.\n" +
               "- Write in English.\n" +
               "- Do not invent topics not present in the conversation.";
    }

    private static string FormatPreviousSummary(MemorySummary ms)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[OVERVIEW]");
        sb.AppendLine(ms.Overview);
        if (ms.TopicMap.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("[TOPIC MAP]");
            foreach (var t in ms.TopicMap)
                sb.AppendLine($"{t.Theme}: msg_range={t.MessageRange} | evolution={t.Evolution} | status={t.Status}");
        }
        sb.AppendLine();
        sb.AppendLine($"[EMOTIONAL ARC] {ms.EmotionalArc}");
        if (!string.IsNullOrWhiteSpace(ms.ClinicalFlags))
        {
            sb.AppendLine();
            sb.AppendLine("[CLINICAL FLAGS]");
            sb.AppendLine(ms.ClinicalFlags);
        }
        if (!string.IsNullOrWhiteSpace(ms.FocusNext))
        {
            sb.AppendLine();
            sb.AppendLine("[FOCUS NEXT]");
            sb.AppendLine(ms.FocusNext);
        }
        return sb.ToString();
    }
}
