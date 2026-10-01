using System.Collections.Frozen;

namespace AilabTemplate.Core;

/// <summary>
/// A deliberately trivial lexicon baseline. It exists so that the HTTP contract, the tests and the
/// eval gate are real from day one; a lab replaces it with a model behind the same <see cref="IScorer"/>.
/// </summary>
/// <remarks>
/// Score = (positiveHits - negativeHits) / (positiveHits + negativeHits). It ignores negation and
/// sarcasm on purpose: the eval fixture contains such rows so the baseline's ceiling is visible.
/// </remarks>
public sealed class KeywordScorer : IScorer
{
    private static readonly FrozenSet<string> Positive = FrozenSet.ToFrozenSet(
    [
        "good", "great", "excellent", "love", "loved", "fast", "quick", "helpful", "happy", "easy",
        "reliable", "clear", "recommend", "perfect", "smooth", "friendly", "amazing", "pleased",
        "resolved", "works", "fixed", "intuitive", "thanks", "thank", "solid", "delighted",
    ], StringComparer.Ordinal);

    private static readonly FrozenSet<string> Negative = FrozenSet.ToFrozenSet(
    [
        "bad", "slow", "broken", "terrible", "hate", "crash", "crashes", "crashed", "confusing",
        "error", "errors", "fails", "failed", "refund", "awful", "poor", "disappointed", "late",
        "missing", "worst", "bug", "buggy", "useless", "frustrating", "unreliable", "lost",
    ], StringComparer.Ordinal);

    public string Name => "keyword-baseline-v1";

    public ScoreResult Score(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var matches = new List<string>();
        var positive = 0;
        var negative = 0;

        foreach (var token in Tokenize(text))
        {
            if (Positive.Contains(token))
            {
                positive++;
                matches.Add(token);
            }
            else if (Negative.Contains(token))
            {
                negative++;
                matches.Add(token);
            }
        }

        var total = positive + negative;
        var score = total == 0 ? 0d : Math.Round((positive - negative) / (double)total, 4);
        var label = score switch
        {
            > 0 => Labels.Positive,
            < 0 => Labels.Negative,
            _ => Labels.Neutral,
        };

        return new ScoreResult(label, score, matches);
    }

    /// <summary>Lower-cases and splits on anything that is not a letter or an apostrophe.</summary>
    internal static IEnumerable<string> Tokenize(string text)
    {
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var isWordChar = i < text.Length && (char.IsLetter(text[i]) || text[i] == '\'');
            if (isWordChar && start < 0)
            {
                start = i;
            }
            else if (!isWordChar && start >= 0)
            {
                yield return text[start..i].Trim('\'').ToLowerInvariant();
                start = -1;
            }
        }
    }
}
