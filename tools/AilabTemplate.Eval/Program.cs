// Offline regression gate for an IScorer: runs every fixture row, writes eval_results.json,
// prints one Markdown table row (derived from that same result) for the README, and exits
// non-zero below the threshold.
//
//   dotnet run --project tools/AilabTemplate.Eval -- \
//     [--fixture fixtures/score_eval.jsonl] [--out eval_results.json] [--threshold 0.80] \
//     [--commit <sha>] [--lab <repo name>]
//
// Exit codes: 0 = at or above threshold, 1 = below threshold, 2 = bad arguments or bad fixture.

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using AilabTemplate.Core;
using AilabTemplate.Eval;

var fixture = "fixtures/score_eval.jsonl";
var output = "eval_results.json";
var threshold = 0.80;
string? commit = Environment.GetEnvironmentVariable("GITHUB_SHA") is { Length: > 0 } sha ? sha : null;
var lab = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") is { Length: > 0 } repo
    ? repo[(repo.LastIndexOf('/') + 1)..]
    : "ailab-template-dotnet";

for (var i = 0; i < args.Length; i++)
{
    var value = i + 1 < args.Length ? args[i + 1] : null;
    switch (args[i])
    {
        case "--fixture" when value is not null: fixture = value; i++; break;
        case "--out" when value is not null: output = value; i++; break;
        case "--commit" when value is not null: commit = value.Length > 0 ? value : null; i++; break;
        case "--lab" when !string.IsNullOrWhiteSpace(value): lab = value; i++; break;
        case "--threshold" when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var t) && t is >= 0 and <= 1:
            threshold = t; i++; break;
        default:
            await Console.Error.WriteLineAsync($"Unknown or invalid argument '{args[i]}'.").ConfigureAwait(false);
            return 2;
    }
}

if (!File.Exists(fixture))
{
    await Console.Error.WriteLineAsync($"Fixture not found: {fixture}").ConfigureAwait(false);
    return 2;
}

// Swap in the IScorer under evaluation here.
var scorer = new KeywordScorer();
var lines = await File.ReadAllLinesAsync(fixture).ConfigureAwait(false);
var misses = new List<EvalMiss>();
var perLabel = new SortedDictionary<string, LabelStats>(StringComparer.Ordinal);
var rows = 0;

for (var n = 0; n < lines.Length; n++)
{
    if (string.IsNullOrWhiteSpace(lines[n]))
    {
        continue;
    }

    EvalRow? row;
    try
    {
        row = JsonSerializer.Deserialize(lines[n], EvalJsonContext.Default.EvalRow);
    }
    catch (JsonException ex)
    {
        await Console.Error.WriteLineAsync($"{fixture}:{n + 1}: invalid JSON ({ex.Message})").ConfigureAwait(false);
        return 2;
    }

    if (row is null || string.IsNullOrWhiteSpace(row.Id) || string.IsNullOrWhiteSpace(row.Text) || string.IsNullOrWhiteSpace(row.Expected))
    {
        await Console.Error.WriteLineAsync($"{fixture}:{n + 1}: each row needs non-empty id, text and expected.").ConfigureAwait(false);
        return 2;
    }

    rows++;
    var predicted = scorer.Score(row.Text).Label;
    var hit = predicted == row.Expected;
    var stats = perLabel.TryGetValue(row.Expected, out var s) ? s : new LabelStats(0, 0);
    perLabel[row.Expected] = stats with { Total = stats.Total + 1, Correct = stats.Correct + (hit ? 1 : 0) };
    if (!hit)
    {
        misses.Add(new EvalMiss(row.Id, row.Expected, predicted));
    }
}

if (rows == 0)
{
    await Console.Error.WriteLineAsync($"Fixture is empty: {fixture}").ConfigureAwait(false);
    return 2;
}

var accuracy = Math.Round((rows - misses.Count) / (double)rows, 4);
var result = new EvalResult(
    SchemaVersion: 1,
    Lab: lab,
    Dataset: Path.GetFileName(fixture),
    Provider: "baseline",
    Model: scorer.Name,
    PrimaryMetric: "accuracy",
    Metrics: new SortedDictionary<string, double>(StringComparer.Ordinal) { ["accuracy"] = accuracy, ["n"] = rows },
    Threshold: threshold,
    Passed: accuracy >= threshold,
    Commit: commit,
    GeneratedAt: DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
    DatasetSha256: Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(fixture).ConfigureAwait(false))),
    PerLabel: perLabel,
    Misses: misses);

await File.WriteAllTextAsync(output, JsonSerializer.Serialize(result, EvalJsonContext.Default.EvalResult)).ConfigureAwait(false);

// The README row is rendered from the same EvalResult that was just written, never recomputed.
var score = result.Metrics[result.PrimaryMetric];
var notes = misses.Count == 0 ? "no misses" : $"misses: {string.Join(", ", misses.Select(m => m.Id))}";
Console.WriteLine("| Date | Commit | Model/Provider | Dataset | Metric | Score | Notes |");
Console.WriteLine("|---|---|---|---|---|---|---|");
Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
    $"| {result.GeneratedAt[..10]} | {(result.Commit is { Length: > 7 } c ? c[..7] : result.Commit ?? "n/a")} | {result.Model} / {result.Provider} | {result.Dataset} (n={rows}) | {result.PrimaryMetric} | {score:0.000} | threshold {result.Threshold:0.00}; {notes} |"));

if (!result.Passed)
{
    await Console.Error.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
        $"FAIL: {result.PrimaryMetric} {score:0.000} is below threshold {result.Threshold:0.00}.")).ConfigureAwait(false);
    return 1;
}

return 0;
