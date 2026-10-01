using AilabTemplate.Api;
using AilabTemplate.Api.Configuration;
using AilabTemplate.Api.Health;
using AilabTemplate.Api.Scoring;
using AilabTemplate.Core;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateSlimBuilder(args);

// ---- Configuration: environment variables only --------------------------------------------
// No appsettings.json. These in-code defaults sit *underneath* every other source, so any
// environment variable (e.g. Logging__LogLevel__Default=Debug) still overrides them.
builder.Configuration.Sources.Insert(0, new MemoryConfigurationSource
{
    InitialData = new Dictionary<string, string?>
    {
        ["Logging:LogLevel:Default"] = "Information",
        ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
    },
});

// ---- Hosting: listen address -------------------------------------------------------------
// ASPNETCORE_URLS wins if set; otherwise PORT (default 8080) on all interfaces.
if (string.IsNullOrWhiteSpace(builder.Configuration[WebHostDefaults.ServerUrlsKey]))
{
    var port = builder.Configuration["PORT"] is { Length: > 0 } p ? p : "8080";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.AddServerHeader = false;
    kestrel.Limits.MaxRequestBodySize = 64 * 1024;
});

// ---- Logging: one JSON object per line on stdout -------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(json =>
{
    json.UseUtcTimestamp = true;
    json.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

// ---- Services ------------------------------------------------------------------------------
builder.Services.AddAilabOptions();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));
builder.Services.AddSingleton<IScorer, KeywordScorer>();
builder.Services.AddSingleton<ReadinessState>();
builder.Services.AddHealthChecks().AddCheck<ReadinessHealthCheck>("readiness", tags: [ReadinessHealthCheck.Tag]);

var app = builder.Build();

// ---- Lifecycle: readiness follows the host --------------------------------------------------
var readiness = app.Services.GetRequiredService<ReadinessState>();
app.Lifetime.ApplicationStarted.Register(() =>
{
    readiness.MarkReady();
    Log.ReadinessChanged(app.Logger, ready: true);
});
app.Lifetime.ApplicationStopping.Register(() =>
{
    readiness.MarkNotReady();
    Log.ReadinessChanged(app.Logger, ready: false);
});

if (!app.Services.GetRequiredService<IOptions<AilabOptions>>().Value.HasApiToken)
{
    Log.ApiTokenMissing(app.Logger, AilabOptions.ApiTokenVariable);
}

// ---- Endpoints -----------------------------------------------------------------------------
// Liveness runs no checks: 200 for as long as the process can serve HTTP at all.
app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false });
// Readiness runs the tagged checks: 503 until started, and again once shutdown begins.
app.MapHealthChecks("/readyz", new HealthCheckOptions { Predicate = c => c.Tags.Contains(ReadinessHealthCheck.Tag) });
app.MapScoreEndpoints();

await app.RunAsync().ConfigureAwait(false);

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host the app in tests.</summary>
public partial class Program;
