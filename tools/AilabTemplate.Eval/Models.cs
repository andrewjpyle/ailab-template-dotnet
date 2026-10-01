using System.Text.Json;
using System.Text.Json.Serialization;

namespace AilabTemplate.Eval;

internal sealed record EvalRow(string? Id, string? Text, string? Expected);

internal sealed record EvalMiss(string Id, string Expected, string Predicted);

internal sealed record LabelStats(int Total, int Correct);

/// <summary>
/// The eval_results.json contract (schema_version 1). Host applications read this file from the CI
/// artifact, so the required keys and their types are stable; extra keys may be added, never renamed.
/// </summary>
internal sealed record EvalResult(
    int SchemaVersion,
    string Lab,
    string Dataset,
    string Provider,
    string Model,
    string PrimaryMetric,
    SortedDictionary<string, double> Metrics,
    double Threshold,
    bool Passed,
    string? Commit,
    string GeneratedAt,
    // Extra (non-contract) detail for humans debugging a regression.
    string DatasetSha256,
    SortedDictionary<string, LabelStats> PerLabel,
    IReadOnlyList<EvalMiss> Misses);

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(EvalRow))]
[JsonSerializable(typeof(EvalResult))]
internal sealed partial class EvalJsonContext : JsonSerializerContext;
