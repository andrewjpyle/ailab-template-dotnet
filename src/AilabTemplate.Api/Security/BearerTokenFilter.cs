using System.Security.Cryptography;
using System.Text;
using AilabTemplate.Api.Configuration;
using Microsoft.Extensions.Options;

namespace AilabTemplate.Api.Security;

/// <summary>
/// Endpoint filter enforcing a single shared bearer token. Deliberately not ASP.NET Core
/// authentication middleware: a sidecar with one caller needs a gate, not an identity system.
/// </summary>
public sealed class BearerTokenFilter(IOptions<AilabOptions> options) : IEndpointFilter
{
    private const string Scheme = "Bearer ";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var expected = options.Value.ApiToken;
        if (string.IsNullOrWhiteSpace(expected))
        {
            // Fail closed: an unconfigured token never means "open to everyone".
            return TypedResults.Json(
                new ErrorResponse("unavailable", $"{AilabOptions.ApiTokenVariable} is not configured."),
                AppJsonContext.Default.ErrorResponse,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        string? header = context.HttpContext.Request.Headers.Authorization;
        if (header is null
            || !header.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)
            || !TokensMatch(header[Scheme.Length..].Trim(), expected))
        {
            context.HttpContext.Response.Headers.WWWAuthenticate = "Bearer";
            return TypedResults.Json(
                new ErrorResponse("unauthorized", "A valid bearer token is required."),
                AppJsonContext.Default.ErrorResponse,
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Constant-time comparison. Both sides are hashed first so the comparison length is fixed and
    /// the token length is not leaked through timing either.
    /// </summary>
    internal static bool TokensMatch(string provided, string expected)
    {
        Span<byte> a = stackalloc byte[SHA256.HashSizeInBytes];
        Span<byte> b = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(provided), a);
        SHA256.HashData(Encoding.UTF8.GetBytes(expected), b);
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
