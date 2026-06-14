using System.Text.RegularExpressions;
using HybridTherapist.Domain.Models;

namespace HybridTherapist.Application.Layers;

public static partial class MemorySummaryParser
{
    [GeneratedRegex(@"^\[([A-Z ]+)\]$", RegexOptions.Multiline)]
    private static partial Regex SectionHeaderRegex();

    [GeneratedRegex(@"\s+", RegexOptions.None, 200)]
    private static partial Regex WhitespaceNormalizeRegex();

    private static string NormalizeHeader(string raw) =>
        WhitespaceNormalizeRegex().Replace(raw.Trim(), " ");

    public static MemorySummary? Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        string? overview = null;
        var topicMap = new List<TopicEntry>();
        string? emotionalArc = null;
        string? clinicalFlags = null;
        string? focusNext = null;

        string? currentHeader = null;
        var currentLines = new List<string>();

        foreach (string line in raw.Split('\n'))
        {
            var match = SectionHeaderRegex().Match(line);
            if (match.Success)
            {
                StoreSection(currentHeader, currentLines,
                    ref overview, topicMap, ref emotionalArc, ref clinicalFlags, ref focusNext);

                currentHeader = NormalizeHeader(match.Groups[1].Value);
                currentLines.Clear();
            }
            else
            {
                currentLines.Add(line);
            }
        }

        StoreSection(currentHeader, currentLines,
            ref overview, topicMap, ref emotionalArc, ref clinicalFlags, ref focusNext);

        if (overview == null) return null;

        return new MemorySummary(
            Overview: overview.Trim(),
            TopicMap: topicMap,
            EmotionalArc: emotionalArc?.Trim() ?? "stable",
            ClinicalFlags: NormalizeFlagSection(clinicalFlags),
            FocusNext: NormalizeFlagSection(focusNext));
    }

    private static string? NormalizeFlagSection(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string trimmed = raw.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return null;
        if (trimmed.Equals("none", StringComparison.OrdinalIgnoreCase)) return null;
        return trimmed;
    }

    private static void StoreSection(
        string? header,
        List<string> lines,
        ref string? overview,
        List<TopicEntry> topicMap,
        ref string? emotionalArc,
        ref string? clinicalFlags,
        ref string? focusNext)
    {
        if (header == null || lines.Count == 0) return;

        string content = string.Join("\n", lines).Trim();
        if (string.IsNullOrWhiteSpace(content)) return;

        switch (header)
        {
            case "OVERVIEW":
                overview = content;
                break;
            case "TOPIC MAP":
                foreach (string line in content.Split('\n'))
                {
                    TopicEntry? entry = ParseTopicLine(line.Trim());
                    if (entry != null) topicMap.Add(entry);
                }
                break;
            case "EMOTIONAL ARC":
                emotionalArc = content;
                break;
            case "CLINICAL FLAGS":
                clinicalFlags = content;
                break;
            case "FOCUS NEXT":
                focusNext = content;
                break;
        }
    }

#pragma warning disable S3776 // String parser — inherent conditional complexity
    private static TopicEntry? ParseTopicLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        int colonIndex = line.IndexOf(": ", StringComparison.Ordinal);
        if (colonIndex < 0) return null;

        string theme = line[..colonIndex].Trim();
        string rest = line[(colonIndex + 2)..].Trim();
        if (string.IsNullOrWhiteSpace(theme)) return null;

        string messageRange = string.Empty;
        string evolution = string.Empty;
        string status = string.Empty;

        string[] parts = rest.Split(" | ", StringSplitOptions.TrimEntries);
        foreach (string part in parts)
        {
            string trimmed = part.Trim();
            if (trimmed.Length == 0) continue;

            int colonPos = trimmed.IndexOf(": ", StringComparison.Ordinal);
            int eqPos = trimmed.IndexOf('=');

            if (colonPos >= 0 && (eqPos < 0 || colonPos <= eqPos))
            {
                string key = trimmed[..colonPos].Trim().ToLowerInvariant();
                string value = trimmed[(colonPos + 2)..].Trim();
                AssignTopicField(key, value, ref messageRange, ref evolution, ref status);
                continue;
            }

            if (eqPos >= 0)
            {
                string key = trimmed[..eqPos].Trim().ToLowerInvariant();
                string value = trimmed[(eqPos + 1)..].Trim();
                AssignTopicField(key, value, ref messageRange, ref evolution, ref status);
                continue;
            }

            // No recognized separator — assign whole part as messageRange if empty
            if (string.IsNullOrWhiteSpace(messageRange))
                messageRange = trimmed;
        }

        return new TopicEntry(theme, messageRange, evolution, status);
    }
#pragma warning restore S3776

    private static void AssignTopicField(
        string key, string value,
        ref string messageRange, ref string evolution, ref string status)
    {
        switch (key)
        {
            case "msg_range":
            case "range":
            case "message_range":
                messageRange = value;
                break;
            case "evolution":
                evolution = value;
                break;
            case "status":
                status = value;
                break;
        }
    }
}
