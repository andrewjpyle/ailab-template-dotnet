using AilabTemplate.Core;

namespace AilabTemplate.Api.Tests;

public sealed class KeywordScorerTests
{
    private readonly KeywordScorer _scorer = new();

    [Theory]
    [InlineData("This is great, I love it", "positive", 1.0)]
    [InlineData("Slow, buggy and the export is broken", "negative", -1.0)]
    [InlineData("The meeting moved to Thursday", "neutral", 0.0)]
    [InlineData("Great product, but the export crashed", "neutral", 0.0)]
    [InlineData("GREAT! Fast! Helpful! One bug.", "positive", 0.5)]
    public void Score_ReturnsExpectedLabelAndPolarity(string text, string label, double score)
    {
        var result = _scorer.Score(text);

        Assert.Equal(label, result.Label);
        Assert.Equal(score, result.Score, precision: 4);
    }

    [Fact]
    public void Score_ReportsMatchesInOrderOfAppearance()
    {
        var result = _scorer.Score("Fast setup, confusing docs, helpful team.");

        Assert.Equal(["fast", "confusing", "helpful"], result.Matches);
    }

    [Fact]
    public void Score_IgnoresNegation_ByDesign()
    {
        // Documents the baseline's known ceiling; a real model should fix this.
        var result = _scorer.Score("not good");

        Assert.Equal("positive", result.Label);
    }

    [Fact]
    public void Score_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => _scorer.Score(null!));

    [Fact]
    public void Tokenize_SplitsOnNonLettersAndKeepsContractions()
    {
        var tokens = KeywordScorer.Tokenize("Don't stop -- it's 'great'!!").ToArray();

        Assert.Equal(["don't", "stop", "it's", "great"], tokens);
    }
}
