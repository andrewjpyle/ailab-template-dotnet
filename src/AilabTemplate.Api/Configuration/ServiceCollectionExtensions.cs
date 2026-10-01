using System.Globalization;
using Microsoft.Extensions.Options;

namespace AilabTemplate.Api.Configuration;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="AilabOptions"/> from flat environment-style keys and validates them at startup,
    /// so a bad value crashes the process on boot instead of on the first request.
    /// </summary>
    public static IServiceCollection AddAilabOptions(this IServiceCollection services)
    {
        services.AddOptions<AilabOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                options.ApiToken = config[AilabOptions.ApiTokenVariable];
                options.MaxTextLength = ReadInt(config, AilabOptions.MaxTextLengthVariable, options.MaxTextLength);
                options.ShutdownTimeoutSeconds = ReadInt(config, AilabOptions.ShutdownTimeoutVariable, options.ShutdownTimeoutSeconds);
            })
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AilabOptions>, AilabOptionsValidator>();

        services.AddOptions<HostOptions>()
            .Configure<IOptions<AilabOptions>>((host, ailab) =>
                host.ShutdownTimeout = TimeSpan.FromSeconds(ailab.Value.ShutdownTimeoutSeconds));

        return services;
    }

    private static int ReadInt(IConfiguration config, string key, int fallback)
    {
        var raw = config[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new OptionsValidationException(key, typeof(AilabOptions), [$"{key} must be an integer, got '{raw}'."]);
    }
}
