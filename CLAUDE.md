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

# Run micro-benchmarks (project is intentionally NOT in the .sln)
dotnet run -c Release --project backend/benchmarks/SmoothOperator.Benchmarks
```

### Performance tooling

```bash
# Frontend bundle analysis — production build + webpack-bundle-analyzer report
cd frontend && npm run analyze

# k6 smoke load test (needs a running stack + k6 installed)
k6 run load-tests/smoke.js
```

### Frontend (Angular 21)

```bash
cd frontend
npm install
npm start          # dev server → http://localhost:4200
npm run build      # production build
npm test                    # Vitest watch mode
npx ng test --watch=false   # single run (CI) — do NOT use `npm test -- --run`, ng test ignores --run
npm run lint                # ESLint via angular-eslint
npx prettier --write .  # format; MUST run before committing any TS/HTML/CSS/JSON change
npm run format:check        # exactly what CI enforces (same binary as the lockfile)
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
3. `MetricsBehavior<,>` — Prometheus duration histogram (`smooth_operator_mediatr_request_duration_seconds`) labelled by request type + success/failure outcome.

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
- **Redis** — registered as singleton `IConnectionMultiplexer`; used for rate limiting and session state. Optionally backs the output cache when `Cache:UseRedis=true` (default: in-memory).
- **Data Protection keys** — persisted to `/data/protection-keys` volume (configurable via `DataProtection__KeysPath`); required to survive container restarts.
- **Docker secrets** — mounted at `/run/secrets/<KEY_NAME>`; use `__` as section separator (e.g., `Jwt__Key` maps to `Jwt:Key` config key).

## Testing Conventions

- Integration tests use `WebApplicationFactory<Program>` + EF Core **in-memory SQLite** for per-test isolation. Do not mock repositories.
- User impersonation: inject `X-Test-UserId` and `X-Test-Roles` headers — no JWT ceremony needed in tests.
- Write endpoints must call `EvictByTagAsync` for their output-cache tags so subsequent GET assertions in the same test see fresh data.
- `SmoothOperator.ArchitectureTests` uses NetArchTest to enforce layer boundaries — violations fail CI.

## Tooling & Workflow

Prefer purpose-built tools over training-data recall. When unsure whether one applies, invoke it — a no-op call is cheaper than a wrong answer.

- **Serena** (`mcp__plugin_serena_serena__*`) — semantic code navigation/editing in this large solution. Call `initial_instructions` first, then `find_symbol`, `find_referencing_symbols`, `get_symbols_overview`, `replace_symbol_body`. Prefer over raw Grep when you care about symbols/structure.
- **Context7** (`mcp__plugin_context7_context7__*`) — version-accurate docs for external libraries (.NET 10, EF Core, MediatR, Mapster, ASP.NET, Angular 21, Tailwind, RxJS, Guacamole, JWT). `resolve-library-id` → `query-docs`; use even when you think you know the answer. Skip for refactors, business-logic debugging, code review, pure language constructs.
- **SonarQube** (`mcp__sonarqube__*` + the `sonarqube:*` skills) — quality gate, coverage gaps, duplications, security hotspots. CI runs SonarCloud at an **80% new-code** gate; check it before opening/merging PRs.
- **GitHub** (`mcp__plugin_github_github__*`, plus `gh` CLI for local git) — PRs, reviews, issues, releases, commit/branch lookups.
- **Linear** (`mcp__claude_ai_Linear__*`) — issue/project tracking when work is tracked there.
- **Web search** — `WebSearch` (general/live web: CVEs, advisories, upstream issues) and `WebFetch` (fetch a specific URL → markdown). Use these, not Context7, for non-library/current info; cite source URLs. (Tavily MCP is optional and not currently connected.)
- **Superpowers skills** — the workflow spine: `brainstorming` before any feature, `test-driven-development`, `systematic-debugging`, `writing-plans`/`executing-plans`, `requesting-code-review`, `verification-before-completion`. Use `frontend-design` when building/restyling Angular UI.

Full per-tool reference (what/when/when-not/how) for contributors: `.github/copilot-instructions.md` (mirrored in the vault's `Reference/Tooling-Guide.md`).

### Project memory

The Obsidian vault `H:\Obsidian\SmoothOperator` is the **canonical** knowledge base — plans, design decisions, diagrams, gotchas, and lessons live there (start at `Home.md`). Claude's slim native `MEMORY.md` auto-loads each session and links into the vault. When saving durable knowledge: write the full note into the vault (`Memory/`, `Plans/`, etc.) and add a one-line pointer to the native `MEMORY.md`. (ContextStream was retired 2026-06-17; do not reintroduce `init`/`context`/`search` MCP calls.)

## Known Gotchas

- **Prettier is CI-enforced** — always run `npx prettier --write .` in `frontend/` before committing any TypeScript, HTML, CSS, SCSS, or JSON change. CI runs the same binary via `npm run format:check`, so the version is single-sourced from `package-lock.json`; `prettier` is pinned exactly (no caret) in `frontend/package.json`. Never hard-pin a different version in `.circleci/config.yml` — the two silently drift and CI then rejects formatting your local run produced. Bumping prettier may require a repo-wide reformat in the same commit.
- **`OidcFlowService`** — always null-check `disco.Issuer` before calling `.TrimEnd('/')` in `ValidateIdTokenAsync`; omitting the check produces a CS8602 warning and a potential `NullReferenceException`.
- **Serilog `DiagnosticContext.Set`** — rejects null values and throws (CS8604 warning path). Guard every `diag.Set("Key", possiblyNullValue)` call in `Program.cs`'s Serilog enrichment lambda.
- **IdentityModel version pinning** — `Microsoft.IdentityModel.*` packages are pinned to `8.18.0` in `SmoothOperator.Infrastructure.csproj` to prevent a `MissingMethodException` caused by a version split between JwtBearer and ITfoxtec.Identity.Saml2. Do not bump these independently.
- **`aria-labelledby` vs `aria-label`** — use `aria-labelledby` (not `aria-label`) to label modal/dialog elements that already have a visible `h2`/`h3` heading inside them.
- Swagger UI is only enabled in `Development` environment. `Metrics__BearerToken` must be set in non-Development/Testing environments or the app throws on startup.
- **SSO output-cache eviction** — `SsoSettingsController` caches `GET /api/settings/sso` under the tag `"sso-settings"` (30 s). Every write endpoint (Toggle, UpsertOidc, UpsertSaml, Delete) must call `await cacheStore.EvictByTagAsync("sso-settings", cancellationToken)` before returning. Forgetting this causes the settings page to blink and show stale state for up to 30 seconds.
- **Auth rate-limit policy scope** — the `"auth"` rate-limit policy (5 req / 60 s per IP) is intentionally **not** applied to `GET /api/auth/setup-status` and `GET /api/auth/providers`. Those are public read-only endpoints called by the Angular app initializer on every full page load. Applying the auth policy to them exhausts the budget before `POST /api/auth/login`, producing a 429 with an empty body and the frontend fallback "Sign-in failed." Only apply `[EnableRateLimiting("auth")]` to credential operations (login, setup, forgot-password).
- **SSO settings → login-page button sync** — after any SSO mutation (toggle, save, remove) the frontend must call `auth.loadSetupStatus()` to refresh `_setup.providers.sso`, which drives the SSO button on the login page. `toggle()` and `remove()` in `sso.ts` already do this; `save()` must do it too. Omitting the call leaves the login-page button stale until the next hard refresh.
- **Backend Docker image is Alpine/musl** — the runtime stage is `aspnet:10.0-alpine`, so `dotnet publish`/`restore` use `-r linux-musl-x64` (the ReadyToRun native code must match the runtime libc) and the image installs `icu-libs` + `tzdata`. Do not change the RID to `linux-x64` without also switching the runtime base back to Debian.
- **nginx container healthchecks must probe `127.0.0.1`, not `localhost`** — `localhost` can resolve to IPv6 `::1`, which nginx's IPv4 `listen 8080` does not bind, so the healthcheck fails and the container is marked unhealthy.
- **`observability/redis.conf` allows no trailing comments** — Redis treats text after a directive value as extra arguments (`FATAL CONFIG FILE ERROR ... wrong number of arguments`). Every comment must be on its own line.
- **Postgres tuning lives in `docker-compose.yml` as `-c` flags**, not a mounted `config_file` — an external `config_file` breaks `hba_file`/`ident_file` path resolution in the official image. `shared_preload_libraries=pg_stat_statements` is set there.
- **Frontend nginx has two config files** — `frontend/nginx-main.conf` is the main context (loads the compiled `ngx_brotli` modules + worker tuning; the `/tmp` temp paths are mandatory under `read_only: true`), and `frontend/nginx.conf` is the server block. The brotli module is compiled from source in a dedicated Dockerfile build stage.
- **Trigram audit-log search** — the `AddTrigramSearchIndexes` migration adds `pg_trgm` expression GIN indexes on `lower(col)`. Keep `GetAuditLogsQuery` text filters as `EF.Functions.Like(col.ToLower(), pattern, "\\")`: this emits `LOWER(col) LIKE …` (uses the index on Postgres) and stays translatable on the SQLite test provider — `EF.Functions.ILike` would break the SQLite integration tests.
- **`SmoothOperator.Benchmarks` is intentionally excluded from `smooth-operator.sln`** so `dotnet test`/CI don't build it. Run it manually with `dotnet run -c Release`.
