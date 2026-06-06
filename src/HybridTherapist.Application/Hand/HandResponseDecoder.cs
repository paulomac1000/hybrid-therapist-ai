using System.Text.RegularExpressions;
using HandCodec.Models;
using HandCodec.Parser;
using RuntimeDecoder = HandRuntime.HandResponseDecoder;
using RuntimeDecodedResponse = HandRuntime.HandDecodedResponse;

namespace HybridTherapist.Application.Hand;

/// <summary>Result of decoding a model response that used the H.A.N.D. wire format.</summary>
public sealed record HandDecodedResponse(
    string Text,
    double Confidence,
    bool HasCrisisSignal,
    int ResilienceLevel)
{
    internal static HandDecodedResponse From(RuntimeDecodedResponse r) =>
        new(r.Text, r.Confidence, r.HasCrisisSignal, r.ResilienceLevel);
}

/// <summary>
/// Application-specific facade. Delegates to <see cref="HandRuntime.HandResponseDecoder"/>
/// but automatically configures the Polish <see cref="CrisisKeywordDetector"/> as a security default.
/// </summary>
public static partial class HandResponseDecoder
{
    private static readonly HandResilientOptions DefaultTherapyOptions = HandResilientOptions.AllEnabled with
    {
        CrisisDetector = CrisisKeywordDetector.DetectCrisisKeywords
    };

    /// <summary>
    /// Strips model-specific thinking/control tokens (e.g. &lt;|control_8|&gt;) from raw LLM output
    /// before handing it to the resilience pipeline. PsychoCounsel 8B emits these blocks
    /// containing internal reasoning — they must never reach the user or the parser.
    /// </summary>
    [GeneratedRegex(@"<\|control_\d+\|>.*?<\|control_\d+\|>\s*", RegexOptions.Singleline, 200)]
    private static partial Regex ControlTokenPattern();

    public static HandDecodedResponse Decode(string rawResponse, AgentClass agentClass)
    {
        string cleaned = ControlTokenPattern().Replace(rawResponse, "");
        RuntimeDecodedResponse result = RuntimeDecoder.Decode(cleaned, agentClass, DefaultTherapyOptions);
        return HandDecodedResponse.From(result);
    }
}
