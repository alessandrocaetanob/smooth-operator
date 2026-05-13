# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Project Is

Smooth Operator is a cloud-native, clientless remote access vault. It provides browser-based RDP/SSH/VNC access via Apache Guacamole — users never see raw credentials; everything is vault-scoped with RBAC.

## Commands

### Backend (.NET 10)

```bash
# Build
dotnet build smooth-operator.sln

# Run (from repo root; Swagger UI available at http://localhost:5000/swagger in Development)
cd backend/src/SmoothOperator.Api && dotnet run

# Run all tests (151 tests: integration + architecture)
dotnet test smooth-operator.sln

# Run a single test project
dotnet test backend/tests/SmoothOperator.Api.Tests

# Format check (CI enforcement)
dotnet format smooth-operator.sln --verify-no-changes

# Add an EF Core migration
dotnet ef migrations add <MigrationName> \
  --project backend/src/SmoothOperator.Infrastructure \
  --startup-project backend/src/SmoothOperator.Api
```

### Frontend (Angular 21)

```bash
cd frontend
npm install
npm start          # dev server → http://localhost:4200
npm run build      # production build
npm test           # Vitest watch mode
npm test -- --run  # single run (CI)
npm run lint       # ESLint via angular-eslint
npx prettier --write .  # format; MUST run before committing any TS/HTML/CSS/JSON change
```

### Docker (full stack)

```bash
docker compose up --build           # core stack (frontend :4200, backend :5000, docs :3000)
docker compose --profile observability up -d   # + Prometheus, Loki, Tempo, Grafana
docker compose --profile code-quality up -d    # + SonarQube :9000
```

## Architecture

### Repository layout

```
backend/     .NET 10 Clean Architecture solution
frontend/    Angular 21 SPA
docs/        Docusaurus 3 docs site
observability/  Prometheus / Loki / Tempo config files
```

### Backend — Clean Architecture + CQRS

Dependency rule: `Api → Application → Domain`; `Infrastructure → Application, Domain`.

- **Domain** — entities, value objects, zero dependencies.
- **Application** — MediatR commands/queries/handlers under `Features/<Domain>/`, FluentValidation validators, Mapster DTOs, Options classes, interface ports. Every feature folder contains `Commands/` and/or `Queries/` subdirectories.
- **Infrastructure** — EF Core `AppDbContext` (PostgreSQL via Npgsql), Redis (StackExchange.Redis), MailKit SMTP, encryption, SSO adapters (OIDC via Duende.IdentityModel, SAML 2.0 via ITfoxtec), Azure Key Vault secret provider.
- **Api** — thin controllers (one `_mediator.Send()` per action), DI composition root split across `Extensions/`, middleware pipeline in `PipelineExtensions.cs`.

MediatR pipeline behaviors (applied to every command/query):
1. `ValidationBehavior<,>` — runs FluentValidation before the handler.
2. `LoggingBehavior<,>` — structured request/response logging.
3. `MetricsBehavior<,>` — Prometheus counter per request type.

Migrations are applied automatically on startup (`ApplyPendingMigrationsAsync` in `Program.cs`). Do not call `dotnet ef database update` manually in production.

### Frontend — Angular 21 SPA

- **Standalone components** throughout; no NgModules.
- All routes are **lazy-loaded** (`loadComponent`) except the eagerly-loaded `Authentication`, `FirstAccess`, `ForgotPassword`, and `Invite` pages.
- Route guards live in `auth.guards.ts`: `rootRedirectGuard`, `loginGuard`, `setupGuard`, `authGuard`, `ownerAdminGuard`, `connectionManagerGuard`.
- **`RuntimeConfigService`** loads `/assets/config.json` on app init so `APP_HELP_URL`, `APP_DOCS_URL`, and feature flags can be overridden per deployment without a rebuild.
- `AuthService` bootstraps on startup: loads setup-status then `me()` (silent-fail).
- JWT is attached by `authInterceptor` (functional interceptor, not class-based).
- **GuacamoleProxyService** (backend singleton) manages the WebSocket relay to guacd. The frontend uses `guacamole-common-js` to render sessions in an HTML5 canvas. Session flow: `POST /api/guacamole/ticket/{id}` → one-time token → `WS /api/guacamole/connect/{id}?ticket=…`.

### Key infrastructure wiring

- **PostgreSQL** — EF Core, `AppDbContext`, aliased as `IAppDbContext` in Application.
- **Redis** — registered as singleton `IConnectionMultiplexer`; used for rate limiting and session state.
- **Data Protection keys** — persisted to `/data/protection-keys` volume (configurable via `DataProtection__KeysPath`); required to survive container restarts.
- **Docker secrets** — mounted at `/run/secrets/<KEY_NAME>`; use `__` as section separator (e.g., `Jwt__Key` maps to `Jwt:Key` config key).

## Testing Conventions

- Integration tests use `WebApplicationFactory<Program>` + EF Core **in-memory SQLite** for per-test isolation. Do not mock repositories.
- User impersonation: inject `X-Test-UserId` and `X-Test-Roles` headers — no JWT ceremony needed in tests.
- Write endpoints must call `EvictByTagAsync` for their output-cache tags so subsequent GET assertions in the same test see fresh data.
- `SmoothOperator.ArchitectureTests` uses NetArchTest to enforce layer boundaries — violations fail CI.

## Known Gotchas

- **Prettier is CI-enforced** — always run `npx prettier --write .` in `frontend/` before committing any TypeScript, HTML, CSS, SCSS, or JSON change.
- **`OidcFlowService`** — always null-check `disco.Issuer` before calling `.TrimEnd('/')` in `ValidateIdTokenAsync`; omitting the check produces a CS8602 warning and a potential `NullReferenceException`.
- **Serilog `DiagnosticContext.Set`** — rejects null values and throws (CS8604 warning path). Guard every `diag.Set("Key", possiblyNullValue)` call in `Program.cs`'s Serilog enrichment lambda.
- **IdentityModel version pinning** — `Microsoft.IdentityModel.*` packages are pinned to `8.18.0` in `SmoothOperator.Infrastructure.csproj` to prevent a `MissingMethodException` caused by a version split between JwtBearer and ITfoxtec.Identity.Saml2. Do not bump these independently.
- **`aria-labelledby` vs `aria-label`** — use `aria-labelledby` (not `aria-label`) to label modal/dialog elements that already have a visible `h2`/`h3` heading inside them.
- Swagger UI is only enabled in `Development` environment. `Metrics__BearerToken` must be set in non-Development/Testing environments or the app throws on startup.
