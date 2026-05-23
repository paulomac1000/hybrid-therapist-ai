using FluentAssertions;
using HybridTherapist.Domain.Services;
using Xunit;

namespace HybridTherapist.Tests;

public sealed class TopicRegistryTests
{
    [Theory]
    [InlineData("nie mogę zasnąć od trzech tygodni", "sleep")]
    [InlineData("mam bezsenność i ciągle się budzę", "sleep")]
    [InlineData("czuję ogromny lęk przed jutrem", "anxiety")]
    [InlineData("mój mąż mnie zdradził", "relationships")]
    [InlineData("po stracie mamy nie umiem żyć dalej", "grief")]
    [InlineData("praca mnie wykończyła, wypaliłam się", "work")]
    [InlineData("I cannot sleep at all", "sleep")]
    [InlineData("just lonely all the time", "loneliness")]
    public void ExtractTopics_DetectsCanonicalTopic(string input, string expectedTopic)
    {
        IReadOnlyList<string> topics = TopicRegistry.ExtractTopics(input);
        topics.Should().Contain(expectedTopic);
    }

    [Fact]
    public void ExtractTopics_MultipleThemes_DeduplicatedInOrder()
    {
        IReadOnlyList<string> topics = TopicRegistry.ExtractTopics(
            "mam lęki i nie mogę zasnąć, czuję się samotna i mąż się mną nie interesuje");
        topics.Should().Contain("anxiety");
        topics.Should().Contain("sleep");
        topics.Should().Contain("loneliness");
        topics.Should().Contain("relationships");
        topics.Distinct().Count().Should().Be(topics.Count);
    }

    [Fact]
    public void ExtractTopics_GenericText_ReturnsEmpty()
    {
        TopicRegistry.ExtractTopics("dziękuję").Should().BeEmpty();
        TopicRegistry.ExtractTopics("").Should().BeEmpty();
    }

    [Fact]
    public void Merge_PreservesOrderAndDeduplicates()
    {
        IReadOnlyList<string> result = TopicRegistry.Merge(
            new[] { "sleep", "anxiety" },
            new[] { "anxiety", "loneliness", "sleep" });
        result.Should().Equal("sleep", "anxiety", "loneliness");
    }
}
