using AilabTemplate.Api.Configuration;
using AilabTemplate.Api.Security;
using AilabTemplate.Core;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace AilabTemplate.Api.Scoring;

public static class ScoreEndpoints
{
    public static IEndpointRouteBuilder MapScoreEndpoints(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/v1").AddEndpointFilter<BearerTokenFilter>();
        v1.MapPost("/score", Score);
        return app;
    }

    internal static Results<Ok<ScoreResponse>, JsonHttpResult<ErrorResponse>> Score(
        ScoreRequest request,
        IScorer scorer,
        IOptions<AilabOptions> options)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("'text' is required and must not be blank.");
        }

        var max = options.Value.MaxTextLength;
        if (request.Text.Length > max)
        {
            return BadRequest($"'text' must be at most {max} characters.");
        }

        var result = scorer.Score(request.Text);
        return TypedResults.Ok(new ScoreResponse(result.Label, result.Score, result.Matches, scorer.Name));
    }

    private static JsonHttpResult<ErrorResponse> BadRequest(string detail) =>
        TypedResults.Json(
            new ErrorResponse("invalid_request", detail),
            AppJsonContext.Default.ErrorResponse,
            statusCode: StatusCodes.Status400BadRequest);
}
