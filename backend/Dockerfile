# syntax=docker/dockerfile:1

# Stage 1: Restore — cached unless .csproj files change.
# The BuildKit cache mount keeps the NuGet package cache warm across builds;
# the same cache is re-mounted in the build/publish stages so --no-restore works.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /repo
COPY ["src/SmoothOperator.Domain/SmoothOperator.Domain.csproj", "src/SmoothOperator.Domain/"]
COPY ["src/SmoothOperator.Application/SmoothOperator.Application.csproj", "src/SmoothOperator.Application/"]
COPY ["src/SmoothOperator.Infrastructure/SmoothOperator.Infrastructure.csproj", "src/SmoothOperator.Infrastructure/"]
COPY ["src/SmoothOperator.Api/SmoothOperator.Api.csproj", "src/SmoothOperator.Api/"]
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "src/SmoothOperator.Api/SmoothOperator.Api.csproj" \
    -r linux-musl-x64 /p:PublishReadyToRun=true

# Stage 2: Build
FROM restore AS build
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet build "src/SmoothOperator.Api/SmoothOperator.Api.csproj" -c Release --no-restore -o /app/build

# Stage 3: Publish — ReadyToRun compiled for musl (linux-musl-x64) to match the
# Alpine runtime image, so the R2R native code is actually usable.
FROM build AS publish
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "src/SmoothOperator.Api/SmoothOperator.Api.csproj" \
    -c Release --no-restore -r linux-musl-x64 --no-self-contained \
    -o /app/publish \
    /p:UseAppHost=false /p:PublishReadyToRun=true

# Stage 4: Runtime — Alpine image (~half the size of the Debian variant).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
EXPOSE 8080

# icu-libs: full globalization (SSO, dates) instead of invariant mode.
# tzdata: timezone database for correct timestamp handling.
# busybox already provides wget for the HEALTHCHECK probe — no curl needed.
RUN apk add --no-cache icu-libs tzdata

COPY --from=publish /app/publish .

# Create the data-protection keys directory owned by the app user so the
# named volume (dp_keys) inherits correct ownership on first mount.
RUN mkdir -p /data/protection-keys && chown "$APP_UID:$APP_UID" /data/protection-keys

# Run as the pre-created non-root user from the .NET base image (uid 1654)
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD wget -q --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "SmoothOperator.Api.dll"]
