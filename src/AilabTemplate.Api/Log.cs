namespace AilabTemplate.Api;

/// <summary>Compile-time generated, allocation-free log messages (CA1848).</summary>
internal static partial class Log
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "{Variable} is not set; protected endpoints will return 503 until it is configured.")]
    public static partial void ApiTokenMissing(ILogger logger, string variable);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Readiness changed to {Ready}.")]
    public static partial void ReadinessChanged(ILogger logger, bool ready);
}
