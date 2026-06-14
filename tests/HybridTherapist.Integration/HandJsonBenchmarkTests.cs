using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace HybridTherapist.Integration;

public sealed class HandJsonBenchmarkTests
{
    private static readonly string[] AnxietyWorry = ["anxiety", "worry"];
    private static readonly string[] Anxiety = ["anxiety"];
    private static readonly string[] Lek = ["lęk"];

    private static readonly BenchmarkTraceEvent OriginalEventL3 = new("L3_supervisor", "input", "{\"approach\":\"grounding\",\"technique\":\"test\",\"key_question\":\"test\"}", "{\"approach\":\"grounding\",\"technique\":\"test\",\"key_question\":\"test\"}", "ok");
    private static readonly BenchmarkTraceEvent OriginalEventL4 = new("L4_therapist", "input", "output", null, "ok");

    private readonly ITestOutputHelper _output;

    public HandJsonBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string CassettePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "Cassettes", filename);

    public static TheoryData<string> DiscoverJsonCassettes()
    {
        var data = new TheoryData<string>();
        string dir = Path.Combine(AppContext.BaseDirectory, "Cassettes");
        if (!Directory.Exists(dir))
            return data;

        foreach (string file in Directory.GetFiles(dir, "json-*.json"))
        {
            data.Add(Path.GetFileName(file));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(DiscoverJsonCassettes))]
    public async Task JsonBenchmark_Scenario_RunsPipeline_MeetsExpectedQuality(string cassetteFile)
    {
        (BenchmarkRun run, BenchmarkExpectations expectations) =
            await HandJsonBenchmarkValidator.RunCassetteAsync(CassettePath(cassetteFile));

        HandJsonBenchmarkValidator.ValidateJsonStrict(run, expectations);
        TokenSavingsMetrics savings = HandJsonBenchmarkValidator.CalculateTokenSavings(run);
        savings.SavingsPercent.Should().BeGreaterThan(0.0,
            "JSON variant must achieve positive token savings");

        _output.WriteLine($"");
        _output.WriteLine($"═══ JSON SCENARIO: {cassetteFile} ═══");
        _output.WriteLine($"  Phase:        {run.Metadata.Phase}");
        _output.WriteLine($"  Approach:     {run.Metadata.SupervisorApproach ?? "N/A"}");
        _output.WriteLine($"  Crisis:       {run.Metadata.CrisisDetected}");
        _output.WriteLine($"  Fallback:     {run.Metadata.Fallback}");
        _output.WriteLine($"  Topics:       [{string.Join(", ", run.Metadata.Topics)}]");
        _output.WriteLine($"  Wire tokens:  ~{savings.WireTokens} (JSON)");
        _output.WriteLine($"  Plain tokens: ~{savings.PlaintextTokens}");
        _output.WriteLine($"  Token save:   ~{savings.TokensSaved} tokens ({savings.SavingsPercent}%)");
        _output.WriteLine($"BENCHMARK_TOKEN_SAVINGS={savings.SavingsPercent:F1}");
        _output.WriteLine($"  Response PL:  {run.Content[..Math.Min(120, run.Content.Length)]}...");
        _output.WriteLine($"");
    }

    [Fact]
    public void JsonBenchmark_Report_AllScenariosFound()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Cassettes");
        if (!Directory.Exists(dir))
        {
            _output.WriteLine("No Cassettes directory found.");
            return;
        }

        string[] cassettes = Directory.GetFiles(dir, "json-*.json");
        _output.WriteLine($"");
        _output.WriteLine($"═══ H.A.N.D. JSON BENCHMARK SUMMARY ═══");
        _output.WriteLine($"  Cassettes found: {cassettes.Length}");
        _output.WriteLine($"  Directory:       {dir}");
        _output.WriteLine($"");

        foreach (string file in cassettes)
        {
            _output.WriteLine($"    - {Path.GetFileNameWithoutExtension(file)}");
        }

        cassettes.Length.Should().BeGreaterThanOrEqualTo(3, "JSON benchmark requires at least 3 scenarios");
    }

    [Fact]
    public void InvalidJson_InL2Response_FailsJsonValidation()
    {
        // Mutated L2 with invalid JSON syntax
        var mutatedEventL2 = new BenchmarkTraceEvent("L2_analyst", "input", "{invalid-json}", "{invalid-json}", "ok");

        var mutatedRun = new BenchmarkRun(
            Content: "Kiedy lęk przejmuje kontrolę, jak się czujesz?",
            Metadata: new BenchmarkMetadata("sess-1", false, false, "INIT", "grounding", AnxietyWorry),
            Events: new[] { mutatedEventL2, OriginalEventL3, OriginalEventL4 }
        );

        var expectations = new BenchmarkExpectations(
            UserInputPl: "test",
            ExpectedPass: true,
            MinQualityScore: 3,
            RequiredTopics: Anxiety,
            RequiredPhrasesPl: Lek,
            ForbiddenPhrases: Array.Empty<string>()
        );

        Action act = () => HandJsonBenchmarkValidator.ValidateJsonStrict(mutatedRun, expectations);
        act.Should().Throw<Exception>("Invalid JSON in L2 response must fail validation");
    }

    [Fact]
    public void MissingRequiredField_InJsonMemo_FailsValidation()
    {
        // Mutated L2 missing "emotional_state" field
        var mutatedEventL2 = new BenchmarkTraceEvent("L2_analyst", "input", "{\"severity\":\"moderate\"}", "{\"severity\":\"moderate\"}", "ok");

        var mutatedRun = new BenchmarkRun(
            Content: "Kiedy lęk przejmuje kontrolę, jak się czujesz?",
            Metadata: new BenchmarkMetadata("sess-1", false, false, "INIT", "grounding", AnxietyWorry),
            Events: new[] { mutatedEventL2, OriginalEventL3, OriginalEventL4 }
        );

        var expectations = new BenchmarkExpectations(
            UserInputPl: "test",
            ExpectedPass: true,
            MinQualityScore: 3,
            RequiredTopics: Anxiety,
            RequiredPhrasesPl: Lek,
            ForbiddenPhrases: Array.Empty<string>()
        );

        Action act = () => HandJsonBenchmarkValidator.ValidateJsonStrict(mutatedRun, expectations);
        act.Should().Throw<Exception>("JSON analyst memo must contain emotional_state");
    }
}
