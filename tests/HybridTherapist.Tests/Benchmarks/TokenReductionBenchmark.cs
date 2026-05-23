using FluentAssertions;
using HandCodec.Parser;
using Xunit;
using Xunit.Abstractions;

namespace HybridTherapist.Tests.Benchmarks;

/// <summary>
/// Measures token reduction achieved by H.A.N.D. wire format vs JSON vs plaintext.
/// Not a runtime benchmark — calculates character/token savings for documentation purposes.
/// </summary>
public sealed class TokenReductionBenchmark
{
    private readonly ITestOutputHelper _output;

    public TokenReductionBenchmark(ITestOutputHelper output) => _output = output;

    [Fact]
    public void MemoFormat_SavesTokensVsJsonAndPlaintext()
    {
        // The same clinical data expressed in three formats:

        // 1. H.A.N.D. M| wire format (actual production format)
        const string handMemo = "M|L=2|em=exhaustion_with_anxiety|sv=moderate|ri=chronic_insomnia|cp=catastrophizing";

        // 2. JSON equivalent
        const string jsonMemo = """
            {"layer":2,"emotional_state":"exhaustion_with_anxiety","severity":"moderate","risk_indicators":"chronic_insomnia","cognitive_patterns":"catastrophizing"}
            """;

        // 3. Plaintext expansion (the old MemoToPlainText format, now removed)
        const string plaintextMemo = """
            Emotional State: exhaustion_with_anxiety
            Severity: moderate
            Risk Indicators: chronic_insomnia
            Cognitive Patterns: catastrophizing
            """;

        int handChars = handMemo.Trim().Length;
        int jsonChars = jsonMemo.Trim().Length;
        int plaintextChars = plaintextMemo.Trim().Length;

        // Approximate token count (1 token ≈ 4 chars for English)
        int handTokens = handChars / 4;
        int jsonTokens = jsonChars / 4;
        int plaintextTokens = plaintextChars / 4;

        _output.WriteLine("=== Token Reduction Benchmark ===");
        _output.WriteLine($"H.A.N.D. M|:  {handChars,4} chars  ~{handTokens,3} tokens");
        _output.WriteLine($"JSON:         {jsonChars,4} chars  ~{jsonTokens,3} tokens");
        _output.WriteLine($"Plaintext:    {plaintextChars,4} chars  ~{plaintextTokens,3} tokens");
        _output.WriteLine($"Savings vs JSON:      {(1.0 - (double)handChars / jsonChars) * 100:F0}%");
        _output.WriteLine($"Savings vs Plaintext: {(1.0 - (double)handChars / plaintextChars) * 100:F0}%");

        // H.A.N.D. must be meaningfully shorter than both alternatives
        handChars.Should().BeLessThan(jsonChars, "M| format should be shorter than JSON");
        handChars.Should().BeLessThan(plaintextChars, "M| format should be shorter than plaintext");
    }

    [Fact]
    public void TwoMemos_InPipeline_ShowCumulativeSavings()
    {
        // Analyst + Supervisor memos in a single pipeline turn
        const string analystMemo = "M|L=2|em=anxiety|sv=moderate|ri=insomnia|cp=catastrophizing";
        const string supervisorMemo = "M|L=3|ap=reflective_listening|tk=open_question|kq=What_keeps_you_up?|rn=none";

        const string analystJson = """{"layer":2,"emotional_state":"anxiety","severity":"moderate","risk_indicators":"insomnia","cognitive_patterns":"catastrophizing"}""";
        const string supervisorJson = """{"layer":3,"approach":"reflective_listening","technique":"open_question","key_question":"What keeps you up?","risk_note":"none"}""";

        const string analystPlain = """
            Emotional State: anxiety
            Severity: moderate
            Risk Indicators: insomnia
            Cognitive Patterns: catastrophizing
            """;
        const string supervisorPlain = """
            Approach: reflective listening
            Technique: open question
            Key Question: What keeps you up?
            Risk Note: none
            """;

        int handTotal = analystMemo.Length + supervisorMemo.Length;
        int jsonTotal = analystJson.Length + supervisorJson.Length;
        int plainTotal = analystPlain.Trim().Length + supervisorPlain.Trim().Length;

        _output.WriteLine("=== Cumulative Pipeline Savings (2 memos) ===");
        _output.WriteLine($"H.A.N.D.:   {handTotal,4} chars  ~{handTotal / 4,3} tokens");
        _output.WriteLine($"JSON:       {jsonTotal,4} chars  ~{jsonTotal / 4,3} tokens");
        _output.WriteLine($"Plaintext:  {plainTotal,4} chars  ~{plainTotal / 4,3} tokens");
        _output.WriteLine($"Token savings vs JSON: ~{(jsonTotal - handTotal) / 4} tokens/turn");

        handTotal.Should().BeLessThan(jsonTotal);
        handTotal.Should().BeLessThan(plainTotal);
    }
}
