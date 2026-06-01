using FluentAssertions;
using Xunit;

namespace HybridTherapist.Integration;

/// <summary>
/// Mutation tests for the H.A.N.D. Codec G benchmark. These tests call the same
/// strict validator as the positive benchmark and verify that known-bad runs fail.
/// </summary>
public sealed class HandBenchmarkNegativeTests
{
    private static string CassettePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "Cassettes", filename);

    [Fact]
    public async Task OldKeysInCassette_FailsCodecGValidation()
    {
        Func<Task> act = async () =>
        {
            (BenchmarkRun run, BenchmarkExpectations expectations) = await RunMutatedCassetteAsync(
                "hand-anxiety.json",
                json => json
                    .Replace("e7=", "em=", StringComparison.Ordinal)
                    .Replace("s9=", "sv=", StringComparison.Ordinal)
                    .Replace("p3=", "ap=", StringComparison.Ordinal)
                    .Replace("t5=", "tk=", StringComparison.Ordinal)
                    .Replace("k2=", "kq=", StringComparison.Ordinal));

            HandBenchmarkValidator.ValidateStrict(run, expectations);
        };

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*L2 must contain e7=*");
    }

    [Fact]
    public async Task MissingL2Memo_FailsBenchmarkValidation()
    {
        Func<Task> act = async () =>
        {
            (BenchmarkRun run, BenchmarkExpectations expectations) = await RunMutatedCassetteAsync(
                "hand-anxiety.json",
                RemoveLayer("L2_analyst"));

            HandBenchmarkValidator.ValidateStrict(run, expectations);
        };

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task FallbackMetadata_FailsBenchmarkValidation()
    {
        (BenchmarkRun run, BenchmarkExpectations expectations) =
            await HandBenchmarkValidator.RunCassetteAsync(CassettePath("hand-anxiety.json"));

        BenchmarkRun fallbackRun = run with
        {
            Metadata = run.Metadata with { Fallback = true },
        };

        Action act = () => HandBenchmarkValidator.ValidateStrict(fallbackRun, expectations);

        act.Should().Throw<Exception>()
            .WithMessage("*must not pass through fallback*");
    }

    [Fact]
    public async Task MissingRequiredPhrase_FailsBenchmarkValidation()
    {
        (BenchmarkRun run, BenchmarkExpectations expectations) =
            await HandBenchmarkValidator.RunCassetteAsync(CassettePath("hand-anxiety.json"));

        BenchmarkRun mutatedRun = run with
        {
            Content = "Słyszę, że to doświadczenie jest bardzo obciążające i zasługuje na uważne zatrzymanie. Co teraz jest dla Ciebie najtrudniejsze?",
        };

        Action act = () => HandBenchmarkValidator.ValidateStrict(mutatedRun, expectations);

        act.Should().Throw<Exception>()
            .WithMessage("*required phrase*");
    }

    [Fact]
    public async Task EnglishFinalResponse_FailsBenchmarkValidation()
    {
        (BenchmarkRun run, BenchmarkExpectations expectations) =
            await HandBenchmarkValidator.RunCassetteAsync(CassettePath("hand-anxiety.json"));

        BenchmarkRun mutatedRun = run with
        {
            Content = "I hear your lęk and your umysł is tired from anxiety. What feels hardest right now?",
        };

        Action act = () => HandBenchmarkValidator.ValidateStrict(mutatedRun, expectations);

        act.Should().Throw<Exception>()
            .WithMessage("*look Polish*");
    }

    [Fact]
    public void LooksPolish_RejectsEnglishTherapyResponse()
    {
        HandBenchmarkValidator.LooksPolish(
                "I hear what you are saying about your anxiety. What feels hardest right now?")
            .Should().BeFalse();
    }

    [Fact]
    public void LooksPolish_AcceptsNaturalPolishTherapyResponse()
    {
        HandBenchmarkValidator.LooksPolish(
                "Słyszę, jak bardzo Cię to wyczerpuje i ile napięcia niesiesz w sobie. Co jest teraz najtrudniejsze?")
            .Should().BeTrue();
    }

    private static async Task<(BenchmarkRun Run, BenchmarkExpectations Expectations)> RunMutatedCassetteAsync(
        string cassetteFile,
        Func<string, string> mutateJson)
    {
        string originalJson = await File.ReadAllTextAsync(CassettePath(cassetteFile));
        string tempPath = Path.GetTempFileName() + ".json";
        await File.WriteAllTextAsync(tempPath, mutateJson(originalJson));

        try
        {
            return await HandBenchmarkValidator.RunCassetteAsync(tempPath);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static Func<string, string> RemoveLayer(string layer)
    {
        return json =>
        {
            string marker = $"\"layer\": \"{layer}\"";
            int markerIndex = json.IndexOf(marker, StringComparison.Ordinal);
            markerIndex.Should().BeGreaterThanOrEqualTo(0, $"cassette must contain {layer}");

            int objectStart = json.LastIndexOf("    {", markerIndex, StringComparison.Ordinal);
            objectStart.Should().BeGreaterThanOrEqualTo(0, $"cassette must expose a JSON object for {layer}");

            int objectEnd = FindMatchingObjectEnd(json, objectStart);
            int removeEnd = objectEnd + 1;
            if (removeEnd < json.Length && json[removeEnd] == ',')
                removeEnd++;
            else
            {
                int previousComma = json.LastIndexOf(',', objectStart);
                previousComma.Should().BeGreaterThanOrEqualTo(0, $"cassette must allow removing {layer}");
                objectStart = previousComma;
            }

            return json.Remove(objectStart, removeEnd - objectStart);
        };
    }

    private static int FindMatchingObjectEnd(string json, int objectStart)
    {
        int depth = 0;
        bool inString = false;
        bool escaped = false;

        for (int i = objectStart; i < json.Length; i++)
        {
            char c = json[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        throw new InvalidDataException("Could not find matching JSON object end.");
    }
}
