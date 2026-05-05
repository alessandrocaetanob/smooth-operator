# Stage 1: Restore — cached unless .csproj files change
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /repo
COPY ["src/SmoothOperator.Domain/SmoothOperator.Domain.csproj", "src/SmoothOperator.Domain/"]
COPY ["src/SmoothOperator.Application/SmoothOperator.Application.csproj", "src/SmoothOperator.Application/"]
COPY ["src/SmoothOperator.Infrastructure/SmoothOperator.Infrastructure.csproj", "src/SmoothOperator.Infrastructure/"]
COPY ["src/SmoothOperator.Api/SmoothOperator.Api.csproj", "src/SmoothOperator.Api/"]
RUN dotnet restore "src/SmoothOperator.Api/SmoothOperator.Api.csproj" \
    -r linux-x64 /p:PublishReadyToRun=true

# Stage 2: Build
FROM restore AS build
COPY . .
RUN dotnet build "src/SmoothOperator.Api/SmoothOperator.Api.csproj" -c Release --no-restore -o /app/build

# Stage 3: Publish
FROM build AS publish
RUN dotnet publish "src/SmoothOperator.Api/SmoothOperator.Api.csproj" \
    -c Release --no-restore -r linux-x64 --no-self-contained \
    -o /app/publish \
    /p:UseAppHost=false /p:PublishReadyToRun=true

# Stage 4: Runtime image — standard Debian image with curl available for health probe
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

# Install curl for the HEALTHCHECK probe (minimal footprint)
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

# Run as the pre-created non-root user from the .NET base image (uid 1654)
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "SmoothOperator.Api.dll"]
