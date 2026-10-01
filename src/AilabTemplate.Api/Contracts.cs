using System.Text.Json;
using System.Text.Json.Serialization;

namespace AilabTemplate.Api;

/// <summary>Request body for <c>POST /v1/score</c>.</summary>
public sealed record ScoreRequest(string? Text);

/// <summary>Response body for <c>POST /v1/score</c>.</summary>
public sealed record ScoreResponse(string Label, double Score, IReadOnlyList<string> Matches, string Model);

/// <summary>Uniform error envelope for every non-2xx response this service writes itself.</summary>
public sealed record ErrorResponse(string Error, string Detail);

/// <summary>
/// System.Text.Json source generation: serializers are emitted at compile time, which is what makes
/// the JSON path reflection-free and therefore Native-AOT and trimming safe.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ScoreRequest))]
[JsonSerializable(typeof(ScoreResponse))]
[JsonSerializable(typeof(ErrorResponse))]
public sealed partial class AppJsonContext : JsonSerializerContext;
