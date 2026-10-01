# syntax=docker/dockerfile:1

# ---- Build: Native AOT needs the platform linker (clang) and zlib headers --------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG TARGETARCH
RUN apt-get update \
 && apt-get install -y --no-install-recommends clang zlib1g-dev \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /src

# Restore first, from project files only, so the layer is cached until a dependency changes.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/AilabTemplate.Core/AilabTemplate.Core.csproj src/AilabTemplate.Core/
COPY src/AilabTemplate.Api/AilabTemplate.Api.csproj src/AilabTemplate.Api/
RUN case "${TARGETARCH:-$(dpkg --print-architecture)}" in \
      amd64) echo x64 ;; \
      arm64) echo arm64 ;; \
      *) echo "unsupported TARGETARCH '${TARGETARCH}'" >&2; exit 1 ;; \
    esac > /tmp/rid-arch \
 && dotnet restore src/AilabTemplate.Api/AilabTemplate.Api.csproj -r "linux-$(cat /tmp/rid-arch)"

COPY src/ src/
RUN dotnet publish src/AilabTemplate.Api/AilabTemplate.Api.csproj \
      -c Release -r "linux-$(cat /tmp/rid-arch)" --no-restore \
      -o /app /p:PublishAot=true \
 && test -x /app/AilabTemplate.Api

# ---- Runtime: chiseled Ubuntu, no shell, no package manager, non-root by default -------------
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app/AilabTemplate.Api ./
# APP_UID (1654, user "app") is defined by the base image; set explicitly so it is visible here.
USER $APP_UID
# The base image sets ASPNETCORE_HTTP_PORTS=8080; clear it so PORT is the single source of truth.
ENV ASPNETCORE_HTTP_PORTS="" \
    PORT=8080
EXPOSE 8080
# No HEALTHCHECK: the image has no shell or curl to run one. Use orchestrator probes instead:
#   liveness  -> GET /healthz    readiness -> GET /readyz
ENTRYPOINT ["./AilabTemplate.Api"]
