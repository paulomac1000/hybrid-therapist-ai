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
/// L3 Supervisor — reads the analyst's M| Memo, picks an approach/technique,
/// and generates its own native M|L=3|ap=...|tk=...|kq=... Memo wire line.
/// Uses Implicit Priming (MemoPing checkpoint). No longer parses plaintext
/// via C# regex — the model emits M| directly.
///
/// MemoToPlainText() has been removed: raw M| enters downstream prompts directly
/// with a dictionary key in the system prompt. This saves ~120 tokens per turn.
///
/// On total decode failure, returns a safe fallback memo.
/// </summary>
public sealed class SupervisorLayer
{
    private readonly IOllamaAdapter _ollama;
    private readonly TherapistOptions _opts;
    private readonly ITraceSink _trace;
    private readonly ILogger<SupervisorLayer> _logger;

    public SupervisorLayer(
        IOllamaAdapter ollama,
        IOptions<TherapistOptions> opts,
        ITraceSink trace,
        ILogger<SupervisorLayer>? logger = null)
    {
        _ollama = ollama;
        _opts = opts.Value;
        _trace = trace;
        _logger = logger ?? NullLogger<SupervisorLayer>.Instance;
    }

    public async Task<SupervisorResult> RunAsync(
        string sessionId,
        string englishUserMessage,
        string analystMemoWire,
        ResponseStrategy strategy,
        CancellationToken ct = default)
    {
        string systemPrompt =
            "You are a clinical supervisor overseeing a therapy session.\n" +
            $"The selected strategy for this turn is: {strategy}.\n\n" +
            "Respond EXACTLY as a single M| wire line. Dictionary:\n" +
            "  L=3 (fixed), ap=approach(CBT|ACT|reflective_listening|...),\n" +
            "  tk=technique(one specific technique),\n" +
            "  kq=key_question(one open question for the therapist),\n" +
            "  rn=risk_note(optional safety note or 'none')\n\n" +
            "Example: M|L=3|ap=CBT|tk=thought_challenging|kq=What evidence supports that thought?|rn=none\n\n" +
            "CRITICAL: Output ONLY one line starting with M|L=3|. Nothing else. " +
            "No markdown. No XML tags. No explanations. Do NOT respond to the user directly.";

        string prompt =
            $"[ANALYST MEMO]\n{analystMemoWire}\n\n" +
            $"User message: {englishUserMessage}";

        IReadOnlyList<HandTurn> messages = HandConversationBuilder.Build(
            systemPrompt,
            HandCheckpointLibrary.TherapySupervisorPing,
            prompt,
            Performative.Memo,
            _opts.AgentClass);

        Stopwatch sw = Stopwatch.StartNew();
        LlmResponse resp = await _ollama.GenerateChatAsync(messages, 200, 0.4f, _opts.Supervisor, ct);
        sw.Stop();

        if (!resp.Ok || string.IsNullOrWhiteSpace(resp.Text))
        {
            await _trace.RecordAsync(new TraceEvent(
                DateTimeOffset.UtcNow, sessionId, "L3_supervisor", _opts.Supervisor,
                prompt, string.Empty, sw.ElapsedMilliseconds, "error", resp.Error), ct);
            return new SupervisorResult(false, "reflective_listening",
                BuildFallbackMemo("llm_error"), resp.Error);
        }

        string sanitized = SanitizeMemoOutput(resp.Text, 3);
        ResilienceResult parsed = HandResiliencePipeline.Parse(sanitized, HandResilientOptions.AllEnabled);
        _logger.LogInformation("[Drabina] L3 resilience level {Level}, confidence {Conf}",
            parsed.Level, parsed.Message.GetDoubleOr("C", 0.5));

        string memo;
        string approach;
        if (parsed.Level >= 5)
        {
            memo = BuildFallbackMemo("decoder_level5_fallback");
            approach = "reflective_listening";
        }
        else if (parsed.Message.Performative == Performative.Memo)
        {
            memo = parsed.Message.RawMessage;
            approach = parsed.Message.Get("ap") ?? "reflective_listening";
        }
        else
        {
            memo = string.IsNullOrWhiteSpace(parsed.Message.Body)
                ? BuildFallbackMemo("parse_no_body")
                : BuildFallbackMemo("parsed_from_body");
            approach = "reflective_listening";
        }

        await _trace.RecordAsync(new TraceEvent(
            DateTimeOffset.UtcNow, sessionId, "L3_supervisor", resp.ModelId ?? _opts.Supervisor,
            prompt, resp.Text, sw.ElapsedMilliseconds, "ok", null, memo), ct);

        return new SupervisorResult(true, approach, memo, null);
    }

    private static string SanitizeMemoOutput(string raw, int expectedLayer)
    {
        string text = raw.Trim();

        int thinkEnd = text.IndexOf(" response", StringComparison.Ordinal);
        if (thinkEnd >= 0) text = text[..thinkEnd].Trim();

        text = text
            .Replace("</think>", "", StringComparison.Ordinal)
            .Replace("<content>", "", StringComparison.OrdinalIgnoreCase)
            .Replace("</content>", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" response", "", StringComparison.Ordinal)
            .Trim();

        if (text.StartsWith($"{expectedLayer}|", StringComparison.Ordinal))
            text = $"M|L={text}";

        return text;
    }

    private string BuildFallbackMemo(string note)
    {
        return new MemoBuilder(_opts.HandCompressionTier)
            .Layer(3)
            .Approach("reflective_listening")
            .Technique("open_question")
            .KeyQuestion("Could you tell me more?")
            .RiskNote("none")
            .Field("note", note)
            .Build();
    }

    public static string ExtractApproach(string memoWire)
    {
        if (string.IsNullOrWhiteSpace(memoWire)) return "unknown";
        ParsedHandMessage? parsed = HandParser.Parse(memoWire);
        return parsed?.Get("ap") ?? "unknown";
    }
}

public sealed record SupervisorResult(bool Ok, string Approach, string Memo, string? Error);
