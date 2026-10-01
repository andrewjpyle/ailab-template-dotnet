using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AilabTemplate.Api.Health;

/// <summary>
/// Process-wide readiness flag. Set when the host has fully started; cleared as soon as shutdown
/// begins so orchestrators stop routing traffic while in-flight requests drain.
/// </summary>
public sealed class ReadinessState
{
    private volatile bool _isReady;

    public bool IsReady => _isReady;

    public void MarkReady() => _isReady = true;

    public void MarkNotReady() => _isReady = false;
}

/// <summary>Reports <see cref="ReadinessState"/> through the standard health-check pipeline.</summary>
public sealed class ReadinessHealthCheck(ReadinessState state) : IHealthCheck
{
    public const string Tag = "ready";

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(state.IsReady
            ? HealthCheckResult.Healthy("Accepting traffic.")
            : HealthCheckResult.Unhealthy("Starting up or shutting down."));
}
