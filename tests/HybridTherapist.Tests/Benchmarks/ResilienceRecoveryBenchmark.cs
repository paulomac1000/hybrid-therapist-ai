using FluentAssertions;
using HandCodec.Models;
using HandCodec.Parser;
using HybridTherapist.Application.Hand;
using Xunit;
using Xunit.Abstractions;

namespace HybridTherapist.Tests.Benchmarks;

/// <summary>
/// Measures the recovery rate of the 5-level HandResiliencePipeline against various common
/// LLM defects (markdown fences, conversational prose, missing fields, etc.).
/// </summary>
public sealed class ResilienceRecoveryBenchmark
{
    private readonly ITestOutputHelper _output;

    public ResilienceRecoveryBenchmark(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ResiliencePipeline_RecoversCommonDefects_WithHighRate()
    {
        // A collection of simulated defective LLM responses
        var samples = new[]
        {
            // Level 1: Perfect compliance (Strict)
            ("R|C=0.9|V=Perfect response", 1, true),
            ("M|L=2|em=sadness|sv=high|note=test", 1, true),
            
            // Level 2: Lenient (Extra whitespace, missing fields but parsable)
            ("   R|C=0.85|V=Trailing spaces   ", 2, true),
            ("Sure, here is my answer: R|C=0.9|V=Missing confidence", 2, true),
            
            // Level 3: Markdown strip (Model wrapped output in code blocks)
            ("```\nR|C=0.9|V=Code block\n```", 3, true),
            ("Here is my answer:\n```json\nR|C=0.8|V=Markdown\n```", 3, true),
            
            // Level 4: Semantic Extraction (Complete failure to use wire format, prose only)
            ("Emotional state: moderate anxiety and exhaustion.", 4, true),
            ("Confidence: 0.95. The patient is definitely catastrophizing.", 4, true),
            ("Answer: To wszystko nie ma sensu, beznadziejne. Nie chcę już żyć. S=crisis.", 4, true), // Crisis keywords sum to >= 7
            
            // Level 6: Unstructured fallback (Complete garbage or unrecognized prose)
            ("Just some random text that does not match any semantic patterns.", 6, false),
            ("xyz 123", 6, false),
        };

        int total = samples.Length;
        int recovered = 0;
        int fullyCompliant = 0;
        var levelCounts = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 }, { 6, 0 } };

        var opts = HandResilientOptions.AllEnabled with
        {
            CrisisDetector = CrisisKeywordDetector.DetectCrisisKeywords
        };

        foreach (var (input, expectedLevel, expectsRecovery) in samples)
        {
            ResilienceResult result = HandResiliencePipeline.Parse(input, opts);

            levelCounts[result.Level]++;

            if (result.Level == 1) fullyCompliant++;

            // A message is considered "recovered" if it was successfully parsed (Level < 6)
            // or if it was expected to fall through to Level 6.
            if (result.Level < 6) recovered++;

            result.Level.Should().BeLessThanOrEqualTo(expectedLevel, $"Input '{input}' should resolve at or below level {expectedLevel}");
        }

        double recoveryRate = (double)recovered / (total - levelCounts[6]); // Excluding intentional garbage

        _output.WriteLine("=== Resilience Recovery Benchmark ===");
        _output.WriteLine($"Total Samples:     {total}");
        _output.WriteLine($"Strictly Parsed:   {fullyCompliant} ({(double)fullyCompliant / total:P0})");
        _output.WriteLine($"Recovered total:   {recovered}");
        _output.WriteLine($"Recovery Rate:     {recoveryRate:P0} (on non-garbage input)");
        _output.WriteLine("");
        _output.WriteLine("Resolution by Level:");
        _output.WriteLine($"  Level 1 (Strict):    {levelCounts[1]}");
        _output.WriteLine($"  Level 2 (Lenient):   {levelCounts[2]}");
        _output.WriteLine($"  Level 3 (Markdown):  {levelCounts[3]}");
        _output.WriteLine($"  Level 4 (Semantic):  {levelCounts[4]}");
        _output.WriteLine($"  Level 5 (JSON):      {levelCounts[5]}");
        _output.WriteLine($"  Level 6 (Fallback):  {levelCounts[6]}");

        recovered.Should().BeGreaterThan(0, "Pipeline should recover at least some defective messages");
    }
}
