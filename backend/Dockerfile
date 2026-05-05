# Stage 1: Restore — cached unless .csproj files change
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /repo
COPY ["src/SmoothOperator.Domain/SmoothOperator.Domain.csproj", "src/SmoothOperator.Domain/"]
COPY ["src/SmoothOperator.Application/SmoothOperator.Application.csproj", "src/SmoothOperator.Application/"]
COPY ["src/SmoothOperator.Infrastructure/SmoothOperator.Infrastructure.csproj", "src/SmoothOperator.Infrastructure/"]
COPY ["src/SmoothOperator.Api/SmoothOperator.Api.csproj", "src/SmoothOperator.Api/"]
RUN dotnet restore "src/SmoothOperator.Api/SmoothOperator.Api.csproj"

# Stage 2: Build
FROM restore AS build
COPY . .
RUN dotnet build "src/SmoothOperator.Api/SmoothOperator.Api.csproj" -c Release --no-restore -o /app/build

# Stage 3: Publish
FROM build AS publish
RUN dotnet publish "src/SmoothOperator.Api/SmoothOperator.Api.csproj" \
    -c Release --no-restore -o /app/publish \
    /p:UseAppHost=false /p:PublishReadyToRun=true

# Stage 4: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmoothOperator.Api.dll"]
