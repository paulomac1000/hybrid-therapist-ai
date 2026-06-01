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
    public static bool StrictCodecG { get; set; }

    /// <summary>
    /// Expands an M| memo wire line into a plaintext English paragraph,
    /// simulating what the prompt would look like without wire compression.
    /// </summary>
    public static string ExpandMemoToPlaintext(string memoWire)
    {
        if (string.IsNullOrWhiteSpace(memoWire) || !memoWire.StartsWith("M|", StringComparison.Ordinal))
            return memoWire;

        string[] parts = memoWire.Split('|');
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string part in parts.Skip(1))
        {
            int eq = part.IndexOf('=');
            if (eq > 0)
                fields[part[..eq].Trim().ToLowerInvariant()] = part[(eq + 1)..].Trim();
        }

        string layer = fields.TryGetValue("l", out var l) ? l : "?";
        var lines = new List<string> { $"Layer {layer} Analysis:" };

        if (fields.TryGetValue("e7", out var em) || (!StrictCodecG && fields.TryGetValue("emotional_state", out em)))
            lines.Add($"- Emotional state: {em}");
        if (fields.TryGetValue("s9", out var sv) || (!StrictCodecG && fields.TryGetValue("severity", out sv)))
            lines.Add($"- Severity: {sv}");
        if (fields.TryGetValue("x4", out var ri) || (!StrictCodecG && fields.TryGetValue("risk_indicators", out ri)))
            lines.Add($"- Risk indicators: {ri}");
        if (fields.TryGetValue("y1", out var cp) || (!StrictCodecG && fields.TryGetValue("cognitive_patterns", out cp)))
            lines.Add($"- Cognitive patterns: {cp}");
        if (fields.TryGetValue("q3", out var ev) || (!StrictCodecG && fields.TryGetValue("evidence_quotes", out ev)))
            lines.Add($"- Evidence: \"{ev}\"");
        if (fields.TryGetValue("p3", out var ap) || (!StrictCodecG && fields.TryGetValue("approach", out ap)))
            lines.Add($"- Approach: {ap}");
        if (fields.TryGetValue("t5", out var tk) || (!StrictCodecG && fields.TryGetValue("technique", out tk)))
            lines.Add($"- Technique: {tk}");
        if (fields.TryGetValue("k2", out var kq) || (!StrictCodecG && fields.TryGetValue("key_question", out kq)))
            lines.Add($"- Key question: {kq}");
        if (fields.TryGetValue("r8", out var rn) || (!StrictCodecG && fields.TryGetValue("risk_note", out rn)))
            lines.Add($"- Risk note: {rn}");

        return string.Join("\n", lines);
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
