namespace HybridTherapist.Application.Hand;

/// <summary>
/// Measures token savings from wire-format compression vs. plaintext expansion.
/// Rough estimate: 1 token ≈ 4 characters for English text.
/// </summary>
public sealed class TokenSavingsTracker
{
    private int _plaintextTokens;
    private int _wireTokens;
    private int _turnCount;

    /// <summary>Records one turn's wire format and its estimated plaintext equivalent.</summary>
    public void Record(string plaintextEquivalent, string wireFormat)
    {
        _plaintextTokens += EstimateTokens(plaintextEquivalent);
        _wireTokens += EstimateTokens(wireFormat);
        _turnCount++;
    }

    public TokenSavingsSummary Summary() => new(
        TotalTurns: _turnCount,
        PlaintextTokensEstimate: _plaintextTokens,
        WireTokensEstimate: _wireTokens,
        TokensSaved: _plaintextTokens - _wireTokens,
        SavingsPercent: _plaintextTokens > 0
            ? Math.Round((1.0 - (double)_wireTokens / _plaintextTokens) * 100, 1)
            : 0);

    /// <summary>
    /// When true, only Codec G random keys (e7,s9,x4,y1,q3,p3,t5,k2,r8) are accepted.
    /// Verbose fallbacks and old keys are treated as failures. Used for research benchmarks.
    /// Default false — allows verbose backwards compatibility in production.
    /// </summary>
    private static readonly AsyncLocal<bool> _strictCodecG = new();

    public static bool StrictCodecG
    {
        get => _strictCodecG.Value;
        set => _strictCodecG.Value = value;
    }

    /// <summary>
    /// Expands an M| memo wire line into a plaintext English paragraph,
    /// simulating what the prompt would look like without wire compression.
    /// </summary>
    public static string ExpandMemoToPlaintext(string memoWire)
    {
        if (string.IsNullOrWhiteSpace(memoWire) || !memoWire.StartsWith("M|", StringComparison.Ordinal))
            return memoWire;

        var fields = ParseMFields(memoWire);

        string layer = fields.TryGetValue("l", out var l) ? l : "?";
        var lines = new List<string> { $"Layer {layer} Analysis:" };

        foreach (var (codecKey, fallbackKey, label) in _fieldDefs)
        {
            if (fields.TryGetValue(codecKey, out var val) || (!StrictCodecG && fields.TryGetValue(fallbackKey, out val)))
                lines.Add($"- {label}: {val}");
        }

        return string.Join("\n", lines);
    }

    private static readonly (string CodecKey, string FallbackKey, string Label)[] _fieldDefs =
    [
        ("e7", "emotional_state", "Emotional state"),
        ("s9", "severity", "Severity"),
        ("x4", "risk_indicators", "Risk indicators"),
        ("y1", "cognitive_patterns", "Cognitive patterns"),
        ("q3", "evidence_quotes", "Evidence"),
        ("p3", "approach", "Approach"),
        ("t5", "technique", "Technique"),
        ("k2", "key_question", "Key question"),
        ("r8", "risk_note", "Risk note"),
    ];

    private static Dictionary<string, string> ParseMFields(string memoWire)
    {
        string[] parts = memoWire.Split('|');
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string part in parts.Skip(1))
        {
            int eq = part.IndexOf('=');
            if (eq > 0)
                fields[part[..eq].Trim().ToLowerInvariant()] = part[(eq + 1)..].Trim();
        }
        return fields;
    }

    private static int EstimateTokens(string text) => Math.Max(1, text.Length / 4);
}

/// <summary>Snapshot of accumulated token savings across all turns.</summary>
public sealed record TokenSavingsSummary(
    int TotalTurns,
    int PlaintextTokensEstimate,
    int WireTokensEstimate,
    int TokensSaved,
    double SavingsPercent);
