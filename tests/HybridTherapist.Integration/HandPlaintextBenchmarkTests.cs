using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace HybridTherapist.Integration;

public sealed class HandPlaintextBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public HandPlaintextBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly string[] AnxietyWorryTopics = ["anxiety", "worry"];
    private static readonly string[] AnxietyTopic = ["anxiety"];
    private static readonly string[] LekPhrase = ["lęk"];

    private static readonly BenchmarkTraceEvent OriginalEventL3 = new("L3_supervisor", "input", "Approach: grounding. Technique: test.", "Approach: grounding. Technique: test.", "ok");
    private static readonly BenchmarkTraceEvent OriginalEventL4 = new("L4_therapist", "input", "output", null, "ok");

    private static string CassettePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "Cassettes", filename);

    public static TheoryData<string> DiscoverPlaintextCassettes()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Cassettes");
        var data = new TheoryData<string>();
        if (!Directory.Exists(dir))
            return data;

        foreach (string file in Directory.GetFiles(dir, "plaintext-*.json"))
        {
            data.Add(Path.GetFileName(file));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(DiscoverPlaintextCassettes))]
    public async Task PlaintextBenchmark_Scenario_RunsPipeline_MeetsExpectedQuality(string cassetteFile)
    {
        (BenchmarkRun run, BenchmarkExpectations expectations) =
            await HandPlaintextBenchmarkValidator.RunCassetteAsync(CassettePath(cassetteFile));

        HandPlaintextBenchmarkValidator.ValidatePlaintextStrict(run, expectations);
        TokenSavingsMetrics savings = HandPlaintextBenchmarkValidator.CalculateTokenSavings(run);
        savings.SavingsPercent.Should().BeLessThan(0.0,
            "Plaintext must be less efficient (negative savings) vs compact baseline");

        _output.WriteLine($"");
        _output.WriteLine($"═══ PLAINTEXT SCENARIO: {cassetteFile} ═══");
        _output.WriteLine($"  Phase:        {run.Metadata.Phase}");
        _output.WriteLine($"  Approach:     {run.Metadata.SupervisorApproach ?? "N/A"}");
        _output.WriteLine($"  Crisis:       {run.Metadata.CrisisDetected}");
        _output.WriteLine($"  Fallback:     {run.Metadata.Fallback}");
        _output.WriteLine($"  Topics:       [{string.Join(", ", run.Metadata.Topics)}]");
        _output.WriteLine($"  Wire tokens:  ~{savings.WireTokens} (plaintext)");
        _output.WriteLine($"  Compact est:  ~{savings.PlaintextTokens}");
        _output.WriteLine($"  Token save:   ~{savings.TokensSaved} tokens ({savings.SavingsPercent}%)");
        _output.WriteLine($"BENCHMARK_TOKEN_SAVINGS={savings.SavingsPercent:F1}");
        _output.WriteLine($"  Response PL:  {run.Content[..Math.Min(120, run.Content.Length)]}...");
        _output.WriteLine($"");
    }

    [Fact]
    public void PlaintextBenchmark_Report_AllScenariosFound()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Cassettes");
        if (!Directory.Exists(dir))
        {
            _output.WriteLine("No Cassettes directory found.");
            return;
        }

        string[] cassettes = Directory.GetFiles(dir, "plaintext-*.json");
        _output.WriteLine($"");
        _output.WriteLine($"═══ H.A.N.D. PLAINTEXT BENCHMARK SUMMARY ═══");
        _output.WriteLine($"  Cassettes found: {cassettes.Length}");
        _output.WriteLine($"  Directory:       {dir}");
        _output.WriteLine($"");

        foreach (string file in cassettes)
        {
            _output.WriteLine($"    - {Path.GetFileNameWithoutExtension(file)}");
        }

        cassettes.Length.Should().BeGreaterThanOrEqualTo(3, "Plaintext benchmark requires at least 3 scenarios");
    }

    [Fact]
    public void WireFormatLeakage_InPlaintextCassette_FailsValidation()
    {
        // Setup a mutated run with M| leakage
        var mutatedEventL2 = new BenchmarkTraceEvent("L2_analyst", "input", "M|L=2|e7=anxiety|s9=moderate", "M|L=2|e7=anxiety|s9=moderate", "ok");

        var mutatedRun = new BenchmarkRun(
            Content: "Kiedy lęk przejmuje kontrolę, jak się czujesz?",
            Metadata: new BenchmarkMetadata("sess-1", false, false, "INIT", "grounding", AnxietyWorryTopics),
            Events: new[] { mutatedEventL2, OriginalEventL3, OriginalEventL4 }
        );

        var expectations = new BenchmarkExpectations(
            UserInputPl: "test",
            ExpectedPass: true,
            MinQualityScore: 3,
            RequiredTopics: AnxietyTopic,
            RequiredPhrasesPl: LekPhrase,
            ForbiddenPhrases: Array.Empty<string>()
        );

        Action act = () => HandPlaintextBenchmarkValidator.ValidatePlaintextStrict(mutatedRun, expectations);
        act.Should().Throw<Exception>("Plaintext analyst memo must not contain M| wire leakage");
    }

    [Fact]
    public void MissingEmotionalStateLabel_FailsPlaintextValidation()
    {
        // Mutated L2 lacking "Emotional state:" label
        var mutatedEventL2 = new BenchmarkTraceEvent("L2_analyst", "input", "State: anxiety. Severity: moderate.", "State: anxiety. Severity: moderate.", "ok");

        var mutatedRun = new BenchmarkRun(
            Content: "Kiedy lęk przejmuje kontrolę, jak się czujesz?",
            Metadata: new BenchmarkMetadata("sess-1", false, false, "INIT", "grounding", AnxietyWorryTopics),
            Events: new[] { mutatedEventL2, OriginalEventL3, OriginalEventL4 }
        );

        var expectations = new BenchmarkExpectations(
            UserInputPl: "test",
            ExpectedPass: true,
            MinQualityScore: 3,
            RequiredTopics: AnxietyTopic,
            RequiredPhrasesPl: LekPhrase,
            ForbiddenPhrases: Array.Empty<string>()
        );

        Action act = () => HandPlaintextBenchmarkValidator.ValidatePlaintextStrict(mutatedRun, expectations);
        act.Should().Throw<Exception>("Plaintext analyst memo must label emotional state");
    }
}
