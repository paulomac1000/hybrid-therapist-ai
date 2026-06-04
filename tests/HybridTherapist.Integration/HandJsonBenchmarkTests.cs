using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace HybridTherapist.Integration;

public sealed class HandJsonBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public HandJsonBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string CassettePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "Cassettes", filename);

    public static IEnumerable<object[]> DiscoverJsonCassettes()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Cassettes");
        if (!Directory.Exists(dir))
            yield break;

        foreach (string file in Directory.GetFiles(dir, "json-*.json"))
        {
            yield return new object[] { Path.GetFileName(file) };
        }
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
        var originalEventL3 = new BenchmarkTraceEvent("L3_supervisor", "input", "{\"approach\":\"grounding\",\"technique\":\"test\",\"key_question\":\"test\"}", "{\"approach\":\"grounding\",\"technique\":\"test\",\"key_question\":\"test\"}", "ok");
        var originalEventL4 = new BenchmarkTraceEvent("L4_therapist", "input", "output", null, "ok");

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

        Action act = () => HandJsonBenchmarkValidator.ValidateJsonStrict(mutatedRun, expectations);
        act.Should().Throw<Exception>("Invalid JSON in L2 response must fail validation");
    }

    [Fact]
    public void MissingRequiredField_InJsonMemo_FailsValidation()
    {
        // Mutated L2 missing "emotional_state" field
        var mutatedEventL2 = new BenchmarkTraceEvent("L2_analyst", "input", "{\"severity\":\"moderate\"}", "{\"severity\":\"moderate\"}", "ok");
        var originalEventL3 = new BenchmarkTraceEvent("L3_supervisor", "input", "{\"approach\":\"grounding\",\"technique\":\"test\",\"key_question\":\"test\"}", "{\"approach\":\"grounding\",\"technique\":\"test\",\"key_question\":\"test\"}", "ok");
        var originalEventL4 = new BenchmarkTraceEvent("L4_therapist", "input", "output", null, "ok");

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

        Action act = () => HandJsonBenchmarkValidator.ValidateJsonStrict(mutatedRun, expectations);
        act.Should().Throw<Exception>("JSON analyst memo must contain emotional_state");
    }
}
