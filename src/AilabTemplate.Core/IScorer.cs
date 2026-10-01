namespace AilabTemplate.Core;

/// <summary>
/// Scores a short piece of text. Implementations must be stateless and thread-safe:
/// a single instance is shared across all concurrent requests.
/// </summary>
public interface IScorer
{
    /// <summary>A stable identifier for the scorer, reported with every result (e.g. "keyword-baseline-v1").</summary>
    string Name { get; }

    /// <summary>Scores <paramref name="text"/>.</summary>
    ScoreResult Score(string text);
}

/// <summary>The outcome of scoring one text.</summary>
/// <param name="Label">Predicted class: <c>positive</c>, <c>negative</c> or <c>neutral</c>.</param>
/// <param name="Score">Signed polarity in [-1, 1]; 0 when no evidence was found.</param>
/// <param name="Matches">The lexicon terms that contributed to the score, in order of appearance.</param>
public sealed record ScoreResult(string Label, double Score, IReadOnlyList<string> Matches);

/// <summary>Well-known label values.</summary>
public static class Labels
{
    public const string Positive = "positive";
    public const string Negative = "negative";
    public const string Neutral = "neutral";
}
