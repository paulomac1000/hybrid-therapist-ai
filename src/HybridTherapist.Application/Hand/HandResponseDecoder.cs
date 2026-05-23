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
public static class HandResponseDecoder
{
    private static readonly HandResilientOptions DefaultTherapyOptions = HandResilientOptions.AllEnabled with
    {
        CrisisDetector = CrisisKeywordDetector.DetectCrisisKeywords
    };

    public static HandDecodedResponse Decode(string rawResponse, AgentClass agentClass)
    {
        RuntimeDecodedResponse result = RuntimeDecoder.Decode(rawResponse, agentClass, DefaultTherapyOptions);
        return HandDecodedResponse.From(result);
    }
}
