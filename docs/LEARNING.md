# Learning notes

The concepts this template is built from, each tied to where it appears in the code. Read it top
to bottom once; afterwards it works as a reference.

## Minimal APIs

Routes are lambdas or static methods mapped directly onto the app (`app.MapPost("/score", Score)`).
Parameters are bound by convention: complex types from the JSON body, registered services from DI,
simple types from route or query. `MapGroup("/v1")` shares a prefix and filters across routes.
Returning `Results<Ok<T>, JsonHttpResult<E>>` (typed results) makes every possible response visible
in the method signature, which helps tests and OpenAPI generation.
See `src/AilabTemplate.Api/Scoring/ScoreEndpoints.cs`.

**Endpoint filters** (`IEndpointFilter`) are the Minimal API equivalent of MVC action filters: they
wrap a handler, can short-circuit with a result, or call `next`. The bearer-token check is one
(`Security/BearerTokenFilter.cs`).

## The Generic Host and dependency injection

`WebApplication` is built on the .NET Generic Host, which owns configuration, logging, DI and the
application lifetime. Services are registered with a lifetime:

- **Singleton**: one instance for the process (`IScorer`, `ReadinessState`). Must be thread-safe.
- **Scoped**: one per request. Never inject a scoped service into a singleton (a "captive dependency").
- **Transient**: a new instance every time it is resolved.

`IHostApplicationLifetime` exposes `ApplicationStarted`, `ApplicationStopping` and
`ApplicationStopped`. On SIGTERM the host fires `ApplicationStopping`, stops accepting connections,
waits for in-flight requests up to `HostOptions.ShutdownTimeout`, then exits.

## Configuration and the options pattern

Configuration is a layered key/value view over sources (here: in-code defaults, then environment
variables, then command line). Later sources win. Env var `A__B` maps to key `A:B`.

The **options pattern** binds configuration to a typed class and injects it:
- `IOptions<T>`: a singleton snapshot read once.
- `IOptionsSnapshot<T>`: recomputed per request (scoped).
- `IOptionsMonitor<T>`: a singleton that sees reloads and raises change notifications.

Validation runs through `IValidateOptions<T>`. `[OptionsValidator]` generates one from data
annotations at compile time, and `ValidateOnStart()` runs it at boot instead of on first access.
See `Configuration/`.

## Health checks

`AddHealthChecks().AddCheck<T>(name, tags)` registers `IHealthCheck` implementations;
`MapHealthChecks(path, options)` exposes them, with `Predicate` choosing which checks a path runs.
Unhealthy results map to 503 by default. Liveness and readiness should be separate endpoints with
different semantics (see DESIGN.md). See `Health/ReadinessState.cs` and `Program.cs`.

## Native AOT and trimming

`PublishAot=true` compiles IL to a native executable ahead of time with ILCompiler. The output has
no JIT and needs no installed .NET runtime. It starts fast and uses less memory, but:

- **Trimming** removes code that static analysis cannot see being used. Reflection over types that
  are only known at runtime breaks, so the analyzers raise `IL2026`/`IL3050` warnings for it.
- **No runtime code generation**: `Reflection.Emit` and dynamic proxies are unavailable.
- The publish is **per runtime identifier** (`osx-arm64`, `linux-x64`) and needs the platform's
  native linker (clang on Linux and macOS).

`IsAotCompatible=true` turns the trimming, AOT and single-file analyzers on during a normal build,
so problems show up before publishing.

## Source-generated System.Text.Json

A `partial class AppJsonContext : JsonSerializerContext` with `[JsonSerializable(typeof(T))]`
attributes makes the compiler emit serializers for each type. At runtime no reflection is needed,
which is required for AOT and faster everywhere. Register it with
`ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default))`,
and pass `AppJsonContext.Default.T` when serializing directly. See `Contracts.cs`.

The same idea appears in `[LoggerMessage]` (logging) and `[OptionsValidator]` (validation): move
work from runtime reflection to compile-time generation.

## Integration tests with WebApplicationFactory

`WebApplicationFactory<Program>` (from `Microsoft.AspNetCore.Mvc.Testing`) boots the real app,
with the real DI graph and middleware, on an in-memory `TestServer`, and hands out an `HttpClient`
wired to it. No ports and no network. Override configuration with
`builder.UseSetting(key, value)` in `ConfigureWebHost`, or swap services with
`ConfigureTestServices`. Top-level-statement apps need `public partial class Program;` so the test
project can name the entry point. Use `IClassFixture<TFactory>` to share one host across a test
class. See `tests/AilabTemplate.Api.Tests/`.

## Interview questions

1. **What is the difference between `AddSingleton`, `AddScoped` and `AddTransient`, and what is a
   captive dependency?** Singleton lives for the process, scoped for one request, transient per
   resolution. A captive dependency is a shorter-lived service injected into a longer-lived one (a
   scoped `DbContext` in a singleton), which silently extends its lifetime and shares it across
   requests and threads. The default service provider can detect this with scope validation.

2. **When would you use `IOptions<T>`, `IOptionsSnapshot<T>` and `IOptionsMonitor<T>`?**
   `IOptions` for values fixed at startup; `IOptionsSnapshot` when a request should see the latest
   values (it is scoped, so not injectable into singletons); `IOptionsMonitor` in singletons that
   must react to reloads via `OnChange`.

3. **Why do liveness and readiness probes need different semantics?** A failing liveness probe
   restarts the container; a failing readiness probe only removes it from load balancing. Putting a
   dependency check under liveness turns a downstream outage into a restart storm.

4. **What happens between SIGTERM and process exit in an ASP.NET Core app?** The host fires
   `ApplicationStopping`, hosted services and the server stop (Kestrel stops accepting new
   connections and drains in-flight requests), bounded by `HostOptions.ShutdownTimeout`, then
   `ApplicationStopped` fires and the process exits. The orchestrator's grace period must exceed
   that timeout or the process is killed mid-drain.

5. **What breaks under Native AOT, and how do you find it before production?** Runtime reflection
   over unknown types, `Reflection.Emit`, dynamic assembly loading and some serializers. Enable
   `IsAotCompatible` (or publish with AOT) and treat `IL2xxx`/`IL3xxx` warnings as errors; replace
   reflection with source generators.

6. **Why use a `JsonSerializerContext`, even without AOT?** It moves type inspection and serializer
   creation to compile time: faster start-up, less allocation, trim safety, and serialization
   contracts that are checked when the code is compiled.

7. **How do you compare secrets safely, and why is `==` wrong?** String equality returns at the
   first differing character, so response timing leaks how much of a guess is correct. Use
   `CryptographicOperations.FixedTimeEquals` over byte spans of equal length; hashing both inputs
   first also hides the secret's length.

8. **What does `WebApplicationFactory<TEntryPoint>` do, and how do you change configuration or
   services for a test?** It starts the real host on an in-memory `TestServer` and creates
   `HttpClient`s for it. Override settings with `UseSetting` in `ConfigureWebHost`, replace services
   with `ConfigureTestServices`, and share an instance per class with `IClassFixture`.

9. **Minimal APIs or controllers: how do you choose?** Minimal APIs suit small services, AOT and
   explicit, low-ceremony routing. Controllers suit large APIs that lean on MVC conventions, model
   binding extensibility, and filters organised by class. Both share routing, DI and middleware, so
   the choice is about structure and fit, not capability.

10. **Why should a missing configuration secret fail closed, and what does that look like here?**
    Failing open turns a deployment mistake into a public endpoint. Here an unset
    `AILAB_API_TOKEN` makes `/v1/*` return 503 for every caller and logs a warning at startup,
    while health endpoints keep working so the instance is still observable.
