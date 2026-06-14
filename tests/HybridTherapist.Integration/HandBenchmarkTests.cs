using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace HybridTherapist.Integration;

/// <summary>
/// H.A.N.D. Codec benchmark — runs ALL hand-*.json cassettes through the full
/// Socrates pipeline and records per-scenario metrics.
///
/// Metrics collected:
///   - Pass/fail against expected quality criteria
///   - Resilience level per layer
///   - Token savings vs plaintext (estimated from wire format)
///   - Crisis detection accuracy
///
/// Usage:
///   dotnet test tests/HybridTherapist.Integration --filter "HandBenchmark"
///
/// The benchmark produces a machine-readable JSON report at the end.
/// </summary>
public sealed class HandBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public HandBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string CassettePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "Cassettes", filename);

    public static TheoryData<string> DiscoverHandCassettes()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Cassettes");
        var data = new TheoryData<string>();
        if (!Directory.Exists(dir))
            return data;

        foreach (string file in Directory.GetFiles(dir, "hand-*.json"))
        {
            string fileName = Path.GetFileName(file);
            if (fileName.StartsWith("hand-semantic-") || fileName == "hand-long-session.json")
                continue;
            data.Add(fileName);
        }

        return data;
    }

    /// <summary>
    /// Runs every hand-*.json cassette through the pipeline and records metrics.
    /// </summary>
    [Theory]
    [MemberData(nameof(DiscoverHandCassettes))]
    public async Task Benchmark_Scenario_RunsPipeline_MeetsExpectedQuality(string cassetteFile)
    {
        (BenchmarkRun run, BenchmarkExpectations expectations) =
            await HandBenchmarkValidator.RunCassetteAsync(CassettePath(cassetteFile));
        HandBenchmarkValidator.ValidateStrict(run, expectations);
        TokenSavingsMetrics savings = HandBenchmarkValidator.CalculateTokenSavings(run);
        savings.SavingsPercent.Should().BeGreaterThan(15.0,
            "Compact variant must save at least 15% tokens vs plaintext expansion");

        // ── Output report ───────────────────────────────────────────────────
        _output.WriteLine($"");
        _output.WriteLine($"═══ SCENARIO: {cassetteFile} ═══");
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

    /// <summary>
    /// Aggregated summary — prints a JSON report of all scenarios.
    /// This method runs LAST and collects results from the test output.
    /// </summary>
    [Fact]
    public void Benchmark_Report_AllScenariosFound()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Cassettes");
        if (!Directory.Exists(dir))
        {
            _output.WriteLine("No Cassettes directory found.");
            return;
        }

        string[] cassettes = Directory.GetFiles(dir, "hand-*.json")
            .Where(f => !Path.GetFileName(f).StartsWith("hand-semantic-") && Path.GetFileName(f) != "hand-long-session.json")
            .ToArray();
        _output.WriteLine($"");
        _output.WriteLine($"═══ H.A.N.D. BENCHMARK SUMMARY ═══");
        _output.WriteLine($"  Cassettes found: {cassettes.Length}");
        _output.WriteLine($"  Directory:       {dir}");
        _output.WriteLine($"");
        _output.WriteLine($"  Scenarios:");
        foreach (string file in cassettes)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            _output.WriteLine($"    - {name}");
        }

        cassettes.Length.Should().BeGreaterThanOrEqualTo(8,
            "benchmark requires at least 8 diverse scenarios for statistical significance");
    }
}
