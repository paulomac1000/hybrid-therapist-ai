using System.Diagnostics;
using HandCodec.Models;
using HandCodec.Parser;
using HybridTherapist.Application.Hand;
using HybridTherapist.Application.Options;
using HybridTherapist.Domain.Interfaces;
using HybridTherapist.Domain.Models;
using HybridTherapist.Infrastructure.Adapters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HybridTherapist.Application.Layers;

/// <summary>
/// L2 Analyst — emotional analysis. Generates a native M| Memo wire line via
/// Implicit Priming (MemoPing checkpoint). No longer uses structured plaintext
/// prompts parsed by C# regex — the model emits M| directly, saving output tokens.
///
/// On total decode failure (resilience level 6/passthrough), returns a safe fallback memo
/// so downstream L3 and L4 never see a broken input.
/// </summary>
public sealed class AnalystLayer
{
    private readonly IOllamaAdapter _ollama;
    private readonly TherapistOptions _opts;
    private readonly ITraceSink _trace;
    private readonly ILogger<AnalystLayer> _logger;

    public AnalystLayer(
        IOllamaAdapter ollama,
        IOptions<TherapistOptions> opts,
        ITraceSink trace,
        ILogger<AnalystLayer>? logger = null)
    {
        _ollama = ollama;
        _opts = opts.Value;
        _trace = trace;
        _logger = logger ?? NullLogger<AnalystLayer>.Instance;
    }

    public async Task<AnalystResult> RunAsync(
        string sessionId,
        string englishUserMessage,
        IReadOnlyList<ChatMessage> history,
        IReadOnlyList<string> activeTopics,
        CancellationToken ct = default)
    {
        string historyContext = history.Count > 0
            ? "CONVERSATION HISTORY:\n" + string.Join("\n", history.TakeLast(6).Select(m => $"{m.Role}: {m.Content}")) + "\n\n"
            : string.Empty;

        string topicsContext = activeTopics.Count > 0
            ? $"Session topics: {string.Join(", ", activeTopics)}\n\n"
            : string.Empty;

        string systemPrompt =
            "You are a clinical mental health analyst.\n\n" +
            topicsContext +
            historyContext +
            "Analyze the user's emotional state, severity, risk indicators, cognitive patterns, " +
            "and provide evidence from their message.\n\n" +
            "Only analyze themes the user EXPLICITLY mentioned or that appear in session topics. " +
            "Do NOT infer or fabricate new themes.";

        HandCheckpoint baseCheckpoint = _opts.HandWireVariant switch
        {
            HandWireVariant.Semantic => HandCheckpointLibrary.TherapyAnalystSemanticPing,
            HandWireVariant.Plaintext => HandCheckpointLibrary.TherapyAnalystPlaintextPing,
            HandWireVariant.Json => HandCheckpointLibrary.TherapyAnalystJsonPing,
            _ => HandCheckpointLibrary.TherapyAnalystPing,
        };

        HandCheckpoint limitedCheckpoint = new HandCheckpoint(
            baseCheckpoint.Exchanges
                .Take(_opts.ImplicitPrimingCheckpointCount)
                .ToArray());

        IReadOnlyList<HandTurn> messages = HandConversationBuilder.Build(
            systemPrompt,
            limitedCheckpoint,
            englishUserMessage,
            Performative.Memo,
            _opts.AgentClass);

        Stopwatch sw = Stopwatch.StartNew();
        LlmResponse resp = await _ollama.GenerateChatAsync(messages, 200, 0.3f, _opts.Analyst, ct);
        sw.Stop();

        if (!resp.Ok || string.IsNullOrWhiteSpace(resp.Text))
        {
            await _trace.RecordAsync(new TraceEvent(
                DateTimeOffset.UtcNow, sessionId, "L2_analyst", _opts.Analyst,
                englishUserMessage, string.Empty, sw.ElapsedMilliseconds, "error", resp.Error), ct);
            return new AnalystResult(false,
                BuildFallbackMemo("llm_error"), resp.Error);
        }

        string sanitized = SanitizeMemoOutput(resp.Text, 2);
        string memo;

        if (_opts.HandWireVariant == HandWireVariant.Plaintext)
        {
            memo = sanitized;
        }
        else if (_opts.HandWireVariant == HandWireVariant.Json)
        {
            var (jsonMemo, parseOk) = ParseJsonMemo(sanitized);
            if (!parseOk)
            {
                await _trace.RecordAsync(new TraceEvent(
                    DateTimeOffset.UtcNow, sessionId, "L2_analyst", resp.ModelId ?? _opts.Analyst,
                    englishUserMessage, resp.Text, sw.ElapsedMilliseconds, "json_parse_error", null, jsonMemo), ct);
                return new AnalystResult(false, jsonMemo, "JSON parse error");
            }
            memo = jsonMemo;
        }
        else
        {
            ResilienceResult parsed = HandResiliencePipeline.Parse(sanitized, HandResilientOptions.AllEnabled);
            _logger.LogInformation("[Drabina] L2 resilience level {Level}, confidence {Conf}",
                parsed.Level, parsed.Message.GetDoubleOr("C", 0.5));

            if (parsed.Level >= 6)
            {
                memo = BuildFallbackMemo("decoder_level6_passthrough");
            }
            else if (parsed.Message.Performative == Performative.Memo)
            {
                memo = parsed.Message.RawMessage;
            }
            else
            {
                string body = parsed.Message.Body;
                memo = string.IsNullOrWhiteSpace(body)
                    ? BuildFallbackMemo("parse_no_body")
                    : BuildFallbackMemo("parsed_from_body");
            }
        }

        await _trace.RecordAsync(new TraceEvent(
            DateTimeOffset.UtcNow, sessionId, "L2_analyst", resp.ModelId ?? _opts.Analyst,
            englishUserMessage, resp.Text, sw.ElapsedMilliseconds, "ok", null, memo), ct);

        return new AnalystResult(true, memo, null);
    }

    private static string SanitizeMemoOutput(string raw, int expectedLayer)
    {
        string text = raw.Trim();

        int thinkEnd = text.IndexOf("<｜end▁of▁thinking｜>", StringComparison.Ordinal);
        if (thinkEnd >= 0) text = text[..thinkEnd].Trim();

        text = text
            .Replace("</think>", "", StringComparison.Ordinal)
            .Replace("<content>", "", StringComparison.OrdinalIgnoreCase)
            .Replace("</content>", "", StringComparison.OrdinalIgnoreCase)
            .Replace("<｜end▁of▁thinking｜>", "", StringComparison.Ordinal)
            .Trim();

        if (text.StartsWith($"{expectedLayer}|", StringComparison.Ordinal))
            text = $"M|L={text}";

        return text;
    }

    private (string memo, bool ok) ParseJsonMemo(string text)
    {
        try
        {
            using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(text.Trim());
            return (text.Trim(), true);
        }
        catch (System.Text.Json.JsonException)
        {
            return (BuildFallbackMemo("json_parse_error"), false);
        }
    }

    private string BuildFallbackMemo(string note)
    {
        if (_opts.HandWireVariant == HandWireVariant.Plaintext)
        {
            return $"Emotional state: unknown. Severity: low. Risk: none. Patterns: none. Evidence: none. Fallback note: {note}.";
        }
        if (_opts.HandWireVariant == HandWireVariant.Json)
        {
            return $"{{\"layer\":2,\"emotional_state\":\"unknown\",\"severity\":\"low\",\"risk\":\"none\",\"patterns\":\"none\",\"evidence\":\"none\",\"note\":\"{note}\"}}";
        }
        if (_opts.HandWireVariant == HandWireVariant.Semantic)
        {
            return $"M|L=2|em=unknown|sv=low|ri=none|cp=none|ev=none|note={note}";
        }

        return new MemoBuilder(_opts.HandCompressionTier)
            .Layer(2)
            .EmotionalState("unknown")
            .Severity("low")
            .Field("note", note)
            .Build();
    }

    private static string TruncateForMemo(string value, int maxLen) =>
        value.Length <= maxLen ? value : value[..maxLen];
}

public sealed record AnalystResult(bool Ok, string Memo, string? Error);
