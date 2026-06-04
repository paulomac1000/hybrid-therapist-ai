using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace HybridTherapist.Integration;

public sealed class HandSemanticBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public HandSemanticBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string CassettePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "Cassettes", filename);

    public static IEnumerable<object[]> DiscoverSemanticCassettes()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Cassettes");
        if (!Directory.Exists(dir))
            yield break;

        foreach (string file in Directory.GetFiles(dir, "hand-semantic-*.json"))
        {
            yield return new object[] { Path.GetFileName(file) };
        }
    }

    [Theory]
    [MemberData(nameof(DiscoverSemanticCassettes))]
    public async Task SemanticBenchmark_Scenario_RunsPipeline_MeetsExpectedQuality(string cassetteFile)
    {
        (BenchmarkRun run, BenchmarkExpectations expectations) =
            await HandSemanticBenchmarkValidator.RunCassetteAsync(CassettePath(cassetteFile));
        
        HandSemanticBenchmarkValidator.ValidateSemanticStrict(run, expectations);
        TokenSavingsMetrics savings = HandSemanticBenchmarkValidator.CalculateTokenSavings(run);
        savings.SavingsPercent.Should().BeGreaterThan(0.0,
            "Semantic variant must achieve positive token savings");

        _output.WriteLine($"");
        _output.WriteLine($"═══ SEMANTIC SCENARIO: {cassetteFile} ═══");
        _output.WriteLine($"  Phase:        {run.Metadata.Phase}");
        _output.WriteLine($"  Approach:     {run.Metadata.SupervisorApproach ?? "N/A"}");
        _output.WriteLine($"  Crisis:       {run.Metadata.CrisisDetected}");
        _output.WriteLine($"  Fallback:     {run.Metadata.Fallback}");
        _output.WriteLine($"  Topics:       [{string.Join(", ", run.Metadata.Topics)}]");
        _output.WriteLine($"  Wire tokens:  ~{savings.WireTokens}");
        _output.WriteLine($"  Plain tokens: ~{savings.PlaintextTokens}");
        _output.WriteLine($"  Token save:   ~{savings.TokensSaved} tokens ({savings.SavingsPercent}%)");
        _output.WriteLine($"BENCHMARK_TOKEN_SAVINGS={savings.SavingsPercent:F1}");
        _output.WriteLine($"  Response PL:  {run.Content[..Math.Min(120, run.Content.Length)]}...");
        _output.WriteLine($"");
    }

    [Fact]
    public void SemanticBenchmark_Report_AllScenariosFound()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Cassettes");
        if (!Directory.Exists(dir))
        {
            _output.WriteLine("No Cassettes directory found.");
            return;
        }

        string[] cassettes = Directory.GetFiles(dir, "hand-semantic-*.json");
        _output.WriteLine($"");
        _output.WriteLine($"═══ H.A.N.D. SEMANTIC BENCHMARK SUMMARY ═══");
        _output.WriteLine($"  Cassettes found: {cassettes.Length}");
        _output.WriteLine($"  Directory:       {dir}");
        _output.WriteLine($"");
        
        foreach (string file in cassettes)
        {
            _output.WriteLine($"    - {Path.GetFileNameWithoutExtension(file)}");
        }

        cassettes.Length.Should().BeGreaterThanOrEqualTo(3, "Semantic benchmark requires at least 3 scenarios");
    }

    [Fact]
    public void CompactKeysInSemanticCassette_FailsSemanticValidation()
    {
        // Setup a mutated run
        var originalEventL2 = new BenchmarkTraceEvent("L2_analyst", "input", "M|L=2|em=anxiety|sv=moderate", "M|L=2|em=anxiety|sv=moderate", "ok");
        var originalEventL3 = new BenchmarkTraceEvent("L3_supervisor", "input", "M|L=3|ap=grounding|tk=test|kq=test|rn=none", "M|L=3|ap=grounding|tk=test|kq=test|rn=none", "ok");
        var originalEventL4 = new BenchmarkTraceEvent("L4_therapist", "M|L=2|em=anxiety|sv=moderate\nM|L=3|ap=grounding|tk=test|kq=test|rn=none", "therapist response?", null, "ok");

        // Mutated L2 using compact keys (e7=, s9=)
        var mutatedEventL2 = new BenchmarkTraceEvent("L2_analyst", "input", "M|L=2|e7=anxiety|s9=moderate", "M|L=2|e7=anxiety|s9=moderate", "ok");

        var mutatedRun = new BenchmarkRun(
            Content: "Kiedy lęk przejmuje kontrolę, jak się czujesz?",
            Metadata: new BenchmarkMetadata("sess-1", false, false, "INIT", "grounding", new[] { "anxiety", "worry" }),
            Events: new[] { mutatedEventL2, originalEventL3, originalEventL4 }
        );

        var expectations = new BenchmarkExpectations(
            UserInputPl: "test",
            ExpectedPass: true,
            MinQualityScore: 3,
            RequiredTopics: new[] { "anxiety" },
            RequiredPhrasesPl: new[] { "lęk" },
            ForbiddenPhrases: Array.Empty<string>()
        );

        Action act = () => HandSemanticBenchmarkValidator.ValidateSemanticStrict(mutatedRun, expectations);
        act.Should().Throw<Exception>("Compact keys (e7) are not allowed in Semantic validation");
    }

    [Fact]
    public void MissingApproachKey_FailsSemanticValidation()
    {
        var originalEventL2 = new BenchmarkTraceEvent("L2_analyst", "input", "M|L=2|em=anxiety|sv=moderate", "M|L=2|em=anxiety|sv=moderate", "ok");
        // Mutated L3 without ap= key
        var mutatedEventL3 = new BenchmarkTraceEvent("L3_supervisor", "input", "M|L=3|tk=test|kq=test|rn=none", "M|L=3|tk=test|kq=test|rn=none", "ok");
        var originalEventL4 = new BenchmarkTraceEvent("L4_therapist", "input", "output", null, "ok");

        var mutatedRun = new BenchmarkRun(
            Content: "Kiedy lęk przejmuje kontrolę, jak się czujesz?",
            Metadata: new BenchmarkMetadata("sess-1", false, false, "INIT", "grounding", new[] { "anxiety", "worry" }),
            Events: new[] { originalEventL2, mutatedEventL3, originalEventL4 }
        );

        var expectations = new BenchmarkExpectations(
            UserInputPl: "test",
            ExpectedPass: true,
            MinQualityScore: 3,
            RequiredTopics: new[] { "anxiety" },
            RequiredPhrasesPl: new[] { "lęk" },
            ForbiddenPhrases: Array.Empty<string>()
        );

        Action act = () => HandSemanticBenchmarkValidator.ValidateSemanticStrict(mutatedRun, expectations);
        act.Should().Throw<Exception>("Supervisor memo must contain approach (ap)");
    }
}
