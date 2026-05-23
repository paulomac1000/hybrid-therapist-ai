using FluentAssertions;
using HybridTherapist.Application.Layers;
using HybridTherapist.Domain.Models;
using Xunit;

namespace HybridTherapist.Tests;

public sealed class MemorySummaryParserTests
{
    [Fact]
    public void Parse_ValidFullOutput_ReturnsAllFields()
    {
        string input = """
            [OVERVIEW]
            User presents with anxiety about work deadlines.

            [TOPIC MAP]
            work_stress: msg1→msg5 | evolution: deadlines→perfectionism | status: active
            insomnia: msg1→msg3 | related: work_stress | status: active

            [EMOTIONAL ARC]
            anxiety(high, msg1-3) → cautious_hope(low, msg4+)

            [CLINICAL FLAGS]
            STUCK: perfectionism — user resists reframing
            RISK: none

            [FOCUS NEXT]
            Explore perfectionism origins gently.
            """;

        var result = MemorySummaryParser.Parse(input);

        result.Should().NotBeNull();
        result!.Overview.Should().Contain("anxiety");
        result.TopicMap.Should().HaveCount(2);
        result.TopicMap[0].Theme.Should().Be("work_stress");
        result.TopicMap[0].Status.Should().Be("active");
        result.TopicMap[0].Evolution.Should().Be("deadlines→perfectionism");
        result.TopicMap[1].Theme.Should().Be("insomnia");
        result.EmotionalArc.Should().Contain("cautious_hope");
        result.ClinicalFlags.Should().NotBeNull();
        result.ClinicalFlags.Should().Contain("STUCK");
        result.FocusNext.Should().Contain("perfectionism");
    }

    [Fact]
    public void Parse_StandardOnly_HasNullFlagsAndFocus()
    {
        string input = """
            [OVERVIEW]
            Brief summary.

            [TOPIC MAP]
            work: msg1→msg3 | evolution: initial | status: active

            [EMOTIONAL ARC]
            neutral
            """;

        var result = MemorySummaryParser.Parse(input);

        result.Should().NotBeNull();
        result!.Overview.Should().Be("Brief summary.");
        result.TopicMap.Should().HaveCount(1);
        result.EmotionalArc.Should().Be("neutral");
        result.ClinicalFlags.Should().BeNull();
        result.FocusNext.Should().BeNull();
    }

    [Fact]
    public void Parse_MissingOverview_ReturnsNull()
    {
        string input = """
            [TOPIC MAP]
            work: msg1→msg3 | evolution: initial | status: active

            [EMOTIONAL ARC]
            neutral
            """;

        var result = MemorySummaryParser.Parse(input);
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_NoneFlags_ReturnsNull()
    {
        string input = """
            [OVERVIEW]
            Summary.

            [TOPIC MAP]
            work: msg1→msg3 | evolution: initial | status: active

            [EMOTIONAL ARC]
            neutral

            [CLINICAL FLAGS]
            none

            [FOCUS NEXT]
            none
            """;

        var result = MemorySummaryParser.Parse(input);

        result.Should().NotBeNull();
        result!.ClinicalFlags.Should().BeNull();
        result.FocusNext.Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsNull()
    {
        var result = MemorySummaryParser.Parse("");
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_TopicMapWithMissingFields_HandlesGracefully()
    {
        string input = """
            [OVERVIEW]
            Summary.

            [TOPIC MAP]
            work: msg1→msg3 | evolution: initial | status: active
            incomplete_theme: msg1

            [EMOTIONAL ARC]
            neutral
            """;

        var result = MemorySummaryParser.Parse(input);

        result.Should().NotBeNull();
        result!.TopicMap.Should().HaveCount(2);
        result.TopicMap[1].Theme.Should().Be("incomplete_theme");
        result.TopicMap[1].MessageRange.Should().Be("msg1");
        result.TopicMap[1].Evolution.Should().BeEmpty();
        result.TopicMap[1].Status.Should().BeEmpty();
    }

    [Fact]
    public void Parse_TopicLine_AlternativeKeys()
    {
        string input = """
            [OVERVIEW]
            Summary.

            [TOPIC MAP]
            anxiety: range=msg1→msg5 | evolution: escalating | status: active

            [EMOTIONAL ARC]
            stable
            """;

        var result = MemorySummaryParser.Parse(input);

        result.Should().NotBeNull();
        result!.TopicMap.Should().HaveCount(1);
        result.TopicMap[0].Theme.Should().Be("anxiety");
        result.TopicMap[0].MessageRange.Should().Be("msg1→msg5");
    }

    [Fact]
    public void Parse_ExtraPreamble_StillParses()
    {
        string input = """
            Here is the structured summary you requested:

            [OVERVIEW]
            User presents with anxiety.
            I hope this helps!

            [TOPIC MAP]
            anxiety: msg1→msg5 | evolution: steady | status: active

            [EMOTIONAL ARC]
            stable
            """;

        var result = MemorySummaryParser.Parse(input);
        result.Should().NotBeNull();
        result!.Overview.Should().Be("User presents with anxiety.\nI hope this helps!");
    }

    [Fact]
    public void Parse_LowercaseHeader_NotRecognized()
    {
        string input = """
            [overview]
            Summary text.

            [topic map]
            anxiety: msg1 | status: active

            [emotional arc]
            stable
            """;

        var result = MemorySummaryParser.Parse(input);
        result.Should().BeNull(
            "lowercase headers should not match — parser is strict to enforce format compliance");
    }

    [Fact]
    public void Parse_MergedSections_StillParsesAvailableSections()
    {
        string input = """
            [OVERVIEW]
            Summary.
            [TOPIC MAP]
            anxiety: msg1 | evolution: steady | status: active
            [EMOTIONAL ARC]
            stable
            """;

        var result = MemorySummaryParser.Parse(input);
        result.Should().NotBeNull();
        result!.Overview.Should().Be("Summary.");
        result.TopicMap.Should().HaveCount(1);
        result.TopicMap[0].Theme.Should().Be("anxiety");
    }

    [Fact]
    public void Parse_ExtraWhitespaceInHeaders_StillParses()
    {
        string input = """
            [OVERVIEW]
            Summary.

            [TOPIC   MAP]
            anxiety: msg1 | status: active

            [EMOTIONAL   ARC]
            stable
            """;

        var result = MemorySummaryParser.Parse(input);
        result.Should().NotBeNull();
        result!.TopicMap.Should().HaveCount(1);
        result.EmotionalArc.Should().Be("stable");
    }
}
