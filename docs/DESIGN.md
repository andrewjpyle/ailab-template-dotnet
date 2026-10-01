# Design

The template optimises for one shape of service: a **narrow, stateless sidecar** that a host
application calls over HTTP to classify or score a small input. Each decision below is judged
against that shape, plus two constraints: it must be **small enough to read in one sitting**, and
safe to publish.

## Decisions

### Minimal API on `WebApplication.CreateSlimBuilder`
A sidecar has a handful of routes. Minimal APIs keep each route next to its handler and avoid
the reflection-heavy MVC pipeline. `CreateSlimBuilder` drops features a sidecar does not need
(HTTPS config, IIS integration, the full config/logging defaults) and is the builder Native AOT is
designed around.

### Native AOT, invariant globalization, source-generated JSON
AOT gives a single native binary with fast start-up and low memory, which matters when many small
sidecars run beside a host. The cost is no runtime reflection, so everything that would normally
reflect is generated at compile time instead:
- **JSON**: `AppJsonContext : JsonSerializerContext` is inserted first in the resolver chain.
- **Options validation**: `[OptionsValidator]` generates the `IValidateOptions<T>` implementation.
- **Logging**: `[LoggerMessage]` generates allocation-free log methods.

`TreatWarningsAsErrors` also covers the trimming/AOT analyzers, so a reflection-dependent change
fails the build or the publish rather than failing at runtime.

### A separate `Core` project
`IScorer` and `KeywordScorer` live in `AilabTemplate.Core`, which has no ASP.NET dependency and is
marked `IsAotCompatible`. The API, the eval tool and the tests all use the same scorer, and the eval
tool does not have to reference a web host.

### Configuration from environment variables only
12-factor: one artifact, configured by its environment. There is no `appsettings.json` to drift
between environments or to leak values into the image. Code defaults sit underneath the
environment, so any variable still overrides them. Options are validated with `ValidateOnStart`,
so a bad value crashes the process at boot, where an orchestrator reports it, instead of at the
first request.

### Bearer token in an endpoint filter, failing closed
There is exactly one trusted caller (the host application), so the requirement is a gate, not an
identity system. An `IEndpointFilter` on the `/v1` route group checks one shared token with a
constant-time comparison (both sides are SHA-256 hashed first, so even the length is not exposed
by timing). If the token is not configured, protected routes return 503 and a warning is logged:
a missing secret must never turn into an open endpoint. Health routes stay unauthenticated so
orchestrators can probe them.

### Liveness and readiness are different questions
`/healthz` runs no checks: it answers "is the process alive?", and a failure means restart it.
`/readyz` runs the checks tagged `ready`: it answers "should traffic be routed here?". Readiness is
set on `ApplicationStarted` and cleared on `ApplicationStopping`, so on SIGTERM the instance drops
out of rotation while Kestrel drains in-flight requests within `HostOptions.ShutdownTimeout`.
Model-loading or dependency checks belong under the `ready` tag, never under liveness.

### Chiseled, non-root runtime image
The final stage is `runtime-deps:9.0-noble-chiseled`: no shell, no package manager, and only the
native libraries the binary needs. `USER $APP_UID` is set explicitly. Because there is no shell,
there is no Docker `HEALTHCHECK`; the probes are meant for the orchestrator. `compose.yaml` also
runs the container read-only with all capabilities dropped.

### Secret wall in two places
A pre-push hook protects the author; CI protects the repository from everyone else, including a
contributor without the hook. Both run the same two checks with the same config. The private
denylist fails closed, because "no patterns configured" and "nothing found" must not look the same.

### An eval gate, not just tests
Unit tests prove the code does what it says; the eval proves the *scorer* is still good enough.
The eval tool writes a versioned `eval_results.json` contract that a host application can consume
from the CI artifact, and it fails the build when the primary metric drops below the threshold.

## Rejected alternatives

| Alternative | Why not here |
|---|---|
| MVC controllers | More ceremony and reflection for a few routes; worse fit for AOT. |
| Blazor, Razor Pages, any UI | A sidecar has no UI; the host application owns presentation. |
| EF Core or any database | The service is stateless by design. A lab that needs state should question that first, then add it in the lab, not the template. |
| ASP.NET Core authentication/authorization (JWT, OIDC) | One trusted caller, one shared secret. Real identity belongs at the host or the network edge. |
| `appsettings.json` and per-environment files | Configuration drift, and a place for secrets to leak into images. |
| Reflection-based `System.Text.Json` and `ValidateDataAnnotations()` | Not trim-safe; replaced by source generators. |
| Swagger / OpenAPI UI | Useful, but adds dependencies and surface. The endpoint table in the README is the contract; a lab can add `Microsoft.AspNetCore.OpenApi` when it has external consumers. |
| Serilog or other logging frameworks | `AddJsonConsole` already gives structured JSON on stdout, which is what log collectors want. |
| Framework-dependent `aspnet` runtime image | Larger image and slower start than a self-contained AOT binary on `runtime-deps`. |
| Alpine (musl) images | Chiseled Ubuntu is Microsoft's smallest supported glibc option and avoids musl differences. |

## What a real lab adds

Kept out of the template on purpose, so each lab adds only what it uses:

- **Resilience** for outbound calls to a model provider: `Microsoft.Extensions.Http.Resilience`
  (Polly v8) with timeouts, retries with jitter, and a circuit breaker.
- **OpenTelemetry** traces and metrics (request latency, scorer latency, label distribution),
  exported with OTLP.
- **Readiness checks** for real dependencies (model loaded, provider reachable) under the `ready` tag.
- **Rate limiting** (`Microsoft.AspNetCore.RateLimiting`) if the host application can burst.
- **A shutdown delay** before Kestrel stops accepting connections, if the load balancer needs time
  to observe `/readyz` going to 503.
- **A larger, versioned eval set**, with per-class metrics tracked over time.
