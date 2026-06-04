using System.Diagnostics;
using System.Text.RegularExpressions;
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
/// and generates its own native M|L=3|p3=...|t5=...|k2=... Codec G memo line.
/// Uses Implicit Priming (MemoPing checkpoint). No longer parses plaintext
/// via C# regex — the model emits M| directly.
///
public partial class SupervisorLayer
{
    private const string DefaultApproach = "behavioral_activation";
    private const string UnknownFallback = "unknown";
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
            $"The selected strategy for this turn is: {strategy}. " +
            "Read the analyst memo and user message, then decide on an approach, " +
            "technique, key question, and risk note. " +
            "Do NOT respond to the user directly.";

        string prompt =
            $"[ANALYST MEMO]\n{analystMemoWire}\n\n" +
            $"User message: {englishUserMessage}";

        HandCheckpoint baseCheckpoint = _opts.HandWireVariant switch
        {
            HandWireVariant.Semantic => HandCheckpointLibrary.TherapySupervisorSemanticPing,
            HandWireVariant.Plaintext => HandCheckpointLibrary.TherapySupervisorPlaintextPing,
            HandWireVariant.Json => HandCheckpointLibrary.TherapySupervisorJsonPing,
            _ => HandCheckpointLibrary.TherapySupervisorPing,
        };

        HandCheckpoint limitedCheckpoint = new HandCheckpoint(
            baseCheckpoint.Exchanges
                .Take(_opts.ImplicitPrimingCheckpointCount)
                .ToArray());

        IReadOnlyList<HandTurn> messages = HandConversationBuilder.Build(
            systemPrompt,
            limitedCheckpoint,
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
            return new SupervisorResult(false, DefaultApproach,
                BuildFallbackMemo("llm_error"), resp.Error);
        }

        string sanitized = SanitizeMemoOutput(resp.Text, 3);
        var (memo, approach) = ParseSupervisorResponse(sanitized);

        await _trace.RecordAsync(new TraceEvent(
            DateTimeOffset.UtcNow, sessionId, "L3_supervisor", resp.ModelId ?? _opts.Supervisor,
            prompt, resp.Text, sw.ElapsedMilliseconds, "ok", null, memo), ct);

        return new SupervisorResult(true, approach, memo, null);
    }

    private (string memo, string approach) ParseSupervisorResponse(string sanitized)
    {
        if (_opts.HandWireVariant == HandWireVariant.Plaintext)
            return (sanitized, ExtractApproachFromPlaintext(sanitized));

        if (_opts.HandWireVariant == HandWireVariant.Json)
        {
            var (jsonMemo, _, jsonApproach) = ParseJsonMemo(sanitized);
            return (jsonMemo, jsonApproach);
        }

        ResilienceResult parsed = HandResiliencePipeline.Parse(sanitized, HandResilientOptions.AllEnabled);
        _logger.LogInformation("[Drabina] L3 resilience level {Level}, confidence {Conf}",
            parsed.Level, parsed.Message.GetDoubleOr("C", 0.5));

        string approachKey = _opts.HandWireVariant == HandWireVariant.Semantic ? "ap" : "p3";

        if (parsed.Level >= 6)
            return (BuildFallbackMemo("decoder_level6_passthrough"), DefaultApproach);

        if (parsed.Message.Performative == Performative.Memo)
        {
            string ap = parsed.Message.Get(approachKey) ?? DefaultApproach;
            string m = parsed.Message.Get(approachKey) is null
                ? BuildFallbackMemo("missing_ap")
                : parsed.Message.RawMessage;
            return (m, ap);
        }

        return (string.IsNullOrWhiteSpace(parsed.Message.Body)
            ? BuildFallbackMemo("parse_no_body")
            : BuildFallbackMemo("parsed_from_body"), DefaultApproach);
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

    private (string memo, bool ok, string approach) ParseJsonMemo(string text)
    {
        try
        {
            using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(text.Trim());
            string approach = DefaultApproach;
            if (doc.RootElement.TryGetProperty("approach", out var prop))
            {
                approach = prop.GetString() ?? DefaultApproach;
            }
            return (text.Trim(), true, approach);
        }
        catch (System.Text.Json.JsonException)
        {
            return (BuildFallbackMemo("json_parse_error"), false, DefaultApproach);
        }
    }

    private string BuildFallbackMemo(string note)
    {
        if (_opts.HandWireVariant == HandWireVariant.Plaintext)
        {
            return $"Approach: behavioral_activation. Technique: schedule_one_small_activity. Key question: What is one tiny thing you could do today that used to bring you joy? Risk note: none. Fallback note: {note}.";
        }
        if (_opts.HandWireVariant == HandWireVariant.Json)
        {
            return $"{{\"layer\":3,\"approach\":\"behavioral_activation\",\"technique\":\"schedule_one_small_activity\",\"key_question\":\"What is one tiny thing you could do today that used to bring you joy?\",\"risk_note\":\"none\",\"note\":\"{note}\"}}";
        }
        if (_opts.HandWireVariant == HandWireVariant.Semantic)
        {
            return $"M|L=3|ap=behavioral_activation|tk=schedule_one_small_activity|kq=What is one tiny thing you could do today that used to bring you joy?|rn=none|note={note}";
        }

        return new MemoBuilder(_opts.HandCompressionTier)
            .Layer(3)
            .Approach(DefaultApproach)
            .Technique("schedule_one_small_activity")
            .KeyQuestion("What is one tiny thing you could do today that used to bring you joy?")
            .RiskNote("none")
            .Field("note", note)
            .Build();
    }

    [GeneratedRegex(@"(?i)\bapproach\s*:\s*([^.,;\r\n]+)", RegexOptions.None, 200)]
    private static partial Regex ApproachPlaintextRegex();

    private static string ExtractApproachFromPlaintext(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return UnknownFallback;

        var match = ApproachPlaintextRegex().Match(text);
        if (match.Success)
        {
            string val = match.Groups[1].Value.Trim();
            int techIdx = val.IndexOf("Technique", StringComparison.OrdinalIgnoreCase);
            if (techIdx >= 0)
            {
                val = val[..techIdx].Trim();
            }
            return string.IsNullOrWhiteSpace(val) ? UnknownFallback : val;
        }

        return UnknownFallback;
    }

    public static string ExtractApproach(string memoWire)
    {
        if (string.IsNullOrWhiteSpace(memoWire)) return UnknownFallback;
        string text = memoWire.Trim();
        if (text.StartsWith('{') && text.EndsWith('}'))
        {
            try
            {
                using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(text);
                return doc.RootElement.GetProperty("approach").GetString() ?? UnknownFallback;
            }
            catch
            {
                return UnknownFallback;
            }
        }
        if (text.Contains("Approach:", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractApproachFromPlaintext(text);
        }
        ParsedHandMessage? parsed = HandParser.Parse(text);
        if (parsed != null)
        {
            return parsed.Get("p3") ?? parsed.Get("ap") ?? UnknownFallback;
        }
        return UnknownFallback;
    }
}

public sealed record SupervisorResult(bool Ok, string Approach, string Memo, string? Error);
