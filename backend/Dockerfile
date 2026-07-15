# syntax=docker/dockerfile:1

# Stage 1: Restore — cached unless .csproj files change.
# The BuildKit cache mount keeps the NuGet package cache warm across builds;
# the same cache is re-mounted in the build/publish stages so --no-restore works.
#
# TARGETARCH is set automatically by `docker buildx build --platform …`; it
# maps to the matching .NET musl RID so ReadyToRun native code is generated
# for the target architecture (amd64 → linux-musl-x64, arm64 → linux-musl-arm64).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
ARG TARGETARCH=amd64
WORKDIR /repo
COPY ["src/SmoothOperator.Domain/SmoothOperator.Domain.csproj", "src/SmoothOperator.Domain/"]
COPY ["src/SmoothOperator.Application/SmoothOperator.Application.csproj", "src/SmoothOperator.Application/"]
COPY ["src/SmoothOperator.Infrastructure/SmoothOperator.Infrastructure.csproj", "src/SmoothOperator.Infrastructure/"]
COPY ["src/SmoothOperator.Api/SmoothOperator.Api.csproj", "src/SmoothOperator.Api/"]
RUN --mount=type=cache,target=/root/.nuget/packages \
    case "$TARGETARCH" in \
      amd64) RID="linux-musl-x64" ;; \
      arm64) RID="linux-musl-arm64" ;; \
      *) echo "Unsupported TARGETARCH: $TARGETARCH" >&2; exit 1 ;; \
    esac && \
    dotnet restore "src/SmoothOperator.Api/SmoothOperator.Api.csproj" \
    -r "$RID" /p:PublishReadyToRun=true

# Stage 2: Build
FROM restore AS build
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet build "src/SmoothOperator.Api/SmoothOperator.Api.csproj" -c Release --no-restore -o /app/build

# Stage 3: Publish — ReadyToRun compiled for the target musl RID so the R2R
# native code matches the Alpine runtime image's architecture.
FROM build AS publish
ARG TARGETARCH=amd64
RUN --mount=type=cache,target=/root/.nuget/packages \
    case "$TARGETARCH" in \
      amd64) RID="linux-musl-x64" ;; \
      arm64) RID="linux-musl-arm64" ;; \
      *) echo "Unsupported TARGETARCH: $TARGETARCH" >&2; exit 1 ;; \
    esac && \
    dotnet publish "src/SmoothOperator.Api/SmoothOperator.Api.csproj" \
    -c Release --no-restore -r "$RID" --no-self-contained \
    -o /app/publish \
    /p:UseAppHost=false /p:PublishReadyToRun=true

# Stage 4: Runtime — Alpine image (~half the size of the Debian variant).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
EXPOSE 8080

# apk upgrade first: base-image Alpine packages accumulate CVEs between
# upstream rebuilds; the Trivy CI gate fails on fixable HIGH/CRITICAL findings.
# icu-libs: full globalization (SSO, dates) instead of invariant mode.
# tzdata: timezone database for correct timestamp handling.
# busybox already provides wget for the HEALTHCHECK probe — no curl needed.
RUN apk upgrade --no-cache && apk add --no-cache icu-libs tzdata

COPY --from=publish /app/publish .

# Create runtime directories:
#  - /data/protection-keys: data-protection keys, owned by the app user so the
#    named volume (dp_keys) inherits correct ownership on first mount.
#  - /var/recordings-temp + /var/recordings + /var/file-transfer-temp: world-writable
#    so the named volumes (recordings_temp / recordings_data / file_transfer_temp) let
#    BOTH this container (app uid 1654) AND the guacd container (uid 1000) write to
#    the shared mount. Paths are kept un-nested — a /var/recordings/temp nested inside
#    /var/recordings would shadow the Dockerfile-created subdir and start the inner
#    volume empty.
RUN mkdir -p /data/protection-keys /var/recordings-temp /var/recordings /var/file-transfer-temp \
    && chown "$APP_UID:$APP_UID" /data/protection-keys \
    && chmod 0777 /var/recordings-temp /var/recordings /var/file-transfer-temp

# Run as the pre-created non-root user from the .NET base image (uid 1654)
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD wget -q --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "SmoothOperator.Api.dll"]
