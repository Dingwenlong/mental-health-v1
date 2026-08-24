using MentalHealth.Infrastructure.Media;

namespace MentalHealth.UnitTests.Analysis;

public sealed class TextFeatureExtractorTests
{
    private readonly TextFeatureExtractor _extractor = new();

    [Theory]
    [InlineData("我并没有想伤害自己", "self_harm_intent", 0d)]
    [InlineData("我已经想好怎么伤害自己", "self_harm_intent", 1d)]
    [InlineData("我觉得没有希望，也不想再见任何人", "hopelessness", 1d)]
    [InlineData("我最近不想见人，也不想回复消息", "social_withdrawal", 1d)]
    public void Text_features_preserve_negation_context(
        string text,
        string code,
        double expected)
    {
        var feature = _extractor.Extract(text).Single(item => item.Code == code);

        Assert.Equal(expected, feature.Value);
        Assert.InRange(feature.Quality, 0d, 1d);
        Assert.Equal("text-rules-v1", feature.ExtractorVersion);
    }

    [Fact]
    public void Matched_feature_keeps_a_character_source_range()
    {
        var feature = _extractor
            .Extract("最近我觉得没有希望。")
            .Single(item => item.Code == "hopelessness");

        Assert.Equal(1d, feature.Value);
        Assert.Matches("^chars:[0-9]+-[0-9]+$", feature.SourceRange);
    }
}
