using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AilabTemplate.Api.Configuration;

/// <summary>
/// Runtime settings. Populated from environment variables only (12-factor) — see
/// <see cref="ServiceCollectionExtensions.AddAilabOptions"/> for the variable names.
/// </summary>
public sealed class AilabOptions
{
    /// <summary>Env var holding the shared bearer token for protected endpoints.</summary>
    public const string ApiTokenVariable = "AILAB_API_TOKEN";

    /// <summary>Env var for the maximum accepted text length, in characters.</summary>
    public const string MaxTextLengthVariable = "AILAB_MAX_TEXT_LENGTH";

    /// <summary>Env var for the graceful-shutdown budget, in seconds.</summary>
    public const string ShutdownTimeoutVariable = "AILAB_SHUTDOWN_TIMEOUT_SECONDS";

    /// <summary>
    /// Shared secret callers must present as <c>Authorization: Bearer &lt;token&gt;</c>.
    /// When unset, protected endpoints fail closed with 503.
    /// </summary>
    public string? ApiToken { get; set; }

    [Range(1, 100_000)]
    public int MaxTextLength { get; set; } = 4_096;

    [Range(1, 300)]
    public int ShutdownTimeoutSeconds { get; set; } = 20;

    public bool HasApiToken => !string.IsNullOrWhiteSpace(ApiToken);
}

/// <summary>Compile-time generated validator: no reflection, so it is trim/AOT safe.</summary>
[OptionsValidator]
public sealed partial class AilabOptionsValidator : IValidateOptions<AilabOptions>;
