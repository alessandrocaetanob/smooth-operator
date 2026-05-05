## 2024-04-27 - Strict Rate Limiting for Authentication Endpoints
**Vulnerability:** The authentication endpoints (`Login` and `ForgotPassword`) in `AuthController.cs` did not have a specific, stricter rate limit policy, making them susceptible to brute force attacks and email spamming despite the global limit of 100 requests per minute.
**Learning:** The application had an existing global rate limiting mechanism set to 100 requests per minute. However, this limit was too generous for authentication endpoints, which require stronger protection. The `AddRateLimiter` can configure named policies using `AddPolicy` allowing more granular control.
**Prevention:** Always define specific rate limiting policies (e.g., 5 requests per minute) for sensitive endpoints like login, password reset, and registration using `[EnableRateLimiting("policy_name")]` alongside a global rate limit.

## 2025-05-24 - DoS Vulnerability via Shared Global Rate Limiter Partition
**Vulnerability:** The global rate limiter in `backend/Program.cs` used a static fallback string (`"unknown"`) as the partition key for all unauthenticated traffic (`httpContext.User.Identity?.Name ?? "unknown"`). This meant that all anonymous requests globally shared a single rate limit bucket of 100 requests per minute. An attacker could trivially exhaust this bucket by spamming any unauthenticated endpoint, effectively causing a Denial of Service (DoS) for all legitimate unauthenticated users (including crucial processes like WebSocket handshakes for Guacamole connections).
**Learning:** Pooling all anonymous or unauthenticated traffic into a single static partition key for rate limiting creates a massive bottleneck and a critical DoS vulnerability vector. Rate limits must be partitioned by an individual identifier, such as the client's IP address, when a user identity is not available.
**Prevention:** When configuring partitioned rate limiters (e.g., `PartitionedRateLimiter.Create`), always ensure the fallback partition key for unauthenticated users uses a client-specific identifier like `httpContext.Connection.RemoteIpAddress?.ToString()` instead of a static string, avoiding global bucket exhaustion by a single bad actor.

## 2026-05-04 - Stored CSV Injection in Audit Logs Export
**Vulnerability:** The `Export` method in `backend/Controllers/AuditLogsController.cs` generates a CSV file of audit logs. The `Csv` serialization method failed to sanitize fields that started with formula characters (`=`, `+`, `-`, `@`) or that had those characters preceded by leading whitespace/control characters (`\t`, `\r`, `\n`, space) that spreadsheet apps silently ignore. An attacker could log malicious payloads in user-controllable fields (like `UserAgent` or `Details`) which would execute as a macro when an administrator exported the logs and opened them in Microsoft Excel.
**Learning:** Exporting user-controlled data to CSV files without sanitization exposes the application to CSV Injection (Formula Injection), which can lead to Remote Code Execution (RCE) on the administrator's local machine when the file is opened in spreadsheet software like Excel. A naive first-character check can be bypassed with leading whitespace that spreadsheet apps discard before formula evaluation.
**Prevention:** Always sanitize data before embedding it into CSV files. Trim leading whitespace/control characters (`\t`, `\r`, `\n`, space) from the value before checking whether the first meaningful character is a formula trigger (`=`, `+`, `-`, `@`). If so, prepend a single quote (`'`) to the original (untrimmed) value to force the spreadsheet application to treat it as a literal string rather than an executable formula.

## 2026-05-04 - Phase 0 Baseline: 142 tests pass on refactor branch
**Summary:** Enterprise refactor branch (`ac/refactor-testing-performance`) established a clean baseline: 142 integration + unit tests all green. Added `FluentAssertions 8.4.0`, `coverlet.msbuild`, `coverlet.runsettings`, `Directory.Build.props`, and root `.editorconfig`. CI updated to collect Cobertura coverage and upload HTML report artifact. Orphan `test_empty_verification.py` deleted.

## 2026-05-04 - Phase 1 Solution Restructure: Clean Architecture 4-project layout
**Summary:** Split monolithic `Backend.csproj` into 4 Clean Architecture layers under `backend/src/`:
- `SmoothOperator.Domain` — entities (Models/), no external deps
- `SmoothOperator.Application` — DTOs, future MediatR contracts
- `SmoothOperator.Infrastructure` — EF Core, migrations, Redis, MailKit, SSO, encryption
- `SmoothOperator.Api` — controllers, middleware, Program.cs

Moved `backend.tests/` → `backend/tests/SmoothOperator.Api.Tests/`; added empty stub projects for Domain, Application, Infrastructure layers. Rewrote `smooth-operator.sln` with 8-project structure in `src/` + `tests/` solution folders. Updated `backend/Dockerfile` with per-csproj COPY layer caching, non-root user, `PublishReadyToRun=true`, entrypoint from `SmoothOperator.Api.dll`. Updated CI workflow + `coverlet.runsettings` to reference new solution/namespaces. Deleted old `backend/Models/`, `backend/Services/`, `backend/Controllers/` etc. and `backend.tests/`. All 142 tests green at phase boundary.

## 2026-05-04 - Phase 2 Options Pattern & Strongly-Typed Configuration
**Summary:** Replaced all `IConfiguration` indexer string reads with 7 `IOptions<T>` classes in `SmoothOperator.Application.Options/`:
- `EncryptionOptions` (IValidatableObject — 64-char hex validation)
- `JwtOptions` (IValidatableObject — ≥32 bytes UTF-8)
- `GuacdOptions` ([Range] on Port 1–65535)
- `AppUrlsOptions`, `AuthOptions`, `OtelOptions`, `RateLimitOptions`

All options registered in `Program.cs` with `ValidateDataAnnotations().ValidateOnStart()` so misconfiguration fails at startup. Flat env-var keys removed from `appsettings.Development.json` (`ENCRYPTION_KEY`, `APP_URL`, `FRONTEND_URL`). `docker-compose.yml` updated to use `Encryption__Key`, `AppUrls__App`, `AppUrls__Frontend`.

Test infrastructure: `TestWebApplicationFactory` uses `PostConfigure<AuthOptions>` in `ConfigureServices` (runs after all config sources) to pin `AllowSelfRegister = false` by default — fixes race against `appsettings.Development.json`. Added 16 options validation unit tests in `SmoothOperator.Application.Tests/Options/` covering `EncryptionOptions`, `JwtOptions`, `GuacdOptions`. All 158 tests green at phase boundary.

## 2026-07-13 - Phase 5 Architecture Tests (NetArchTest.Rules)
**Summary:** Added `SmoothOperator.ArchitectureTests` project (`backend/tests/SmoothOperator.ArchitectureTests/`) using `NetArchTest.Rules 1.3.2`. Project references all 4 src layers (Domain, Application, Infrastructure, Api).

**9 architecture tests enforcing layer dependency rules:**
1. Domain → must NOT reference Application
2. Domain → must NOT reference Infrastructure
3. Domain → must NOT reference Api
4. Application → must NOT reference Infrastructure
5. Application → must NOT reference Api
6. Infrastructure → must NOT reference Api
7. Controllers → must reside in `SmoothOperator.Api.Controllers` namespace
8. MediatR handlers → must reside under `SmoothOperator.Application.Features.*`
9. Application interfaces → must reside in `SmoothOperator.Application.*`

## 2026-07-13 - Phase 6 Frontend Runtime Config & Core Folder

**Summary:** Established Angular `core/config/` structure and runtime configuration system:

- `RuntimeConfigService` (`src/app/core/config/runtime-config.service.ts`) — fetches `/config/config.json` via native `fetch` in an `APP_INITIALIZER`, exposes `helpUrl`, `docsUrl`, `featureFlags`. Falls back to compile-time defaults on network errors; never blocks bootstrap.
- `public/config/config.json` — default values served for local dev (`localhost:3000`). In Docker containers, overridden at runtime by `entrypoint.sh`.
- `entrypoint.sh` — generates `config.json` from `APP_HELP_URL` / `APP_DOCS_URL` / `APP_FEATURE_FLAGS` env vars at container startup, then execs nginx. Same Docker image now works across all deployment environments without rebuild.
- `app.config.ts` — runtime config `APP_INITIALIZER` runs first, before theme/auth initializers.
- Removed both hardcoded `http://localhost:3000` occurrences: `SideNavBar.helpUrl` and `authentication.html` `href` now read from `RuntimeConfigService`.
- `frontend/Dockerfile` — switched from `CMD` to `ENTRYPOINT ["/entrypoint.sh"]`; added `HEALTHCHECK`.

All 55 Angular unit tests pass. Build clean at commit `fd451f9`.

## 2026-07-13 - Phase 7 Documentation & Code Quality

**Summary:** Completed documentation and code-quality pass.

**Frontend:**
- Created `frontend/eslint.config.js` with `@angular-eslint/recommended` + `typescript-eslint` rules; fixed all flagged issues across 50+ files. `ng lint` passes clean.
- Created `frontend/vitest.config.ts` with `pool: 'forks'` — canonical fix for Vitest 4.x worker_threads hang on Windows. Angular builder picks it up via `"runnerConfig": true` in `angular.json`. All 23 spec files / 55 tests pass.
- Coverage baseline: **39.5% lines / 44.28% branches / 25.3% functions / 40.98% statements** (Phase 8 target: 70%).

**Backend:**
- Moved `SonarAnalyzer.CSharp 10.25.0.139117` from `SmoothOperator.Application.csproj` to root `Directory.Build.props` so it applies to all 4 src projects and test projects. Build: 32 warnings, 0 errors. All 169 tests green.

**Documentation:**
- 6 ADRs written under `docs/adr/`:
  - `0001-clean-architecture.md`
  - `0002-cqrs-with-mediatr.md`
  - `0003-options-pattern.md`
  - `0004-frontend-runtime-config.md`
  - `0005-coverage-gates.md`
  - `0006-frontend-feature-modules.md`
- Architecture overview created at `docs/architecture/overview.md` with Mermaid C4 diagrams: System Context, Container, Component (backend), request flow sequence diagram, and frontend component graph.

## 2026-05-05 - Phase 8: Testing & Coverage Gate

**Summary:** Added 7 new spec files, fixed 3 test failures, established coverage floor.

- **ThemeService fix:** `init()` now always defaults to `'dark'` (removed `prefersDark` check) — fixes 2 failing `theme.service.spec.ts` tests that expected dark-first UX regardless of system preference.
- **New spec files (6 page components + 1 service):**
  - `connections.spec.ts` — `should create` with `provideHttpClient` + `provideRouter`
  - `credentials.spec.ts`, `invite.spec.ts`, `monitoring.spec.ts`, `my-access.spec.ts`, `profile.spec.ts`
  - `motion.service.spec.ts` — 3 tests: reducedMotion true/false + signal updates on change
- **MotionService fix:** `window.matchMedia` is `undefined` in jsdom — replaced `vi.spyOn(window, 'matchMedia')` with direct assignment (same pattern as `theme.service.spec.ts`). Added `TestBed.configureTestingModule` per-test for fresh instances.
- **Coverage config:** Added `coverage` block to `vitest.config.ts` (v8 provider, text+lcov reporters); conservative regression thresholds set: `statements: 40 / branches: 46 / functions: 33 / lines: 42`.
- **Test count:** 240 passing (45 test files), 0 failures.
- **Coverage baseline:** 40.25% stmts / 46.85% branches / 33.62% funcs / 42.35% lines (70% CI gate deferred).

## 2026-07-13 - Phase 10: Performance Pass

**Summary:** Applied performance improvements across backend and Angular frontend.

**Backend:**
- Added `AsNoTracking()` to all 16 read-only EF Core query handlers across Domain, Application, and Infrastructure layers.
- Added `AddResponseCompression()` (Brotli + Gzip) middleware; inserted `app.UseResponseCompression()` before routing.
- Added `AddOutputCache(ShortCache=30s)` service and `app.UseOutputCache()` middleware; applied `[OutputCache(PolicyName="ShortCache")]` to `SystemSettingsController.Get()` and `SsoSettingsController.Get()`.
- Extended OpenTelemetry with `WithMetrics()` chain including `AddAspNetCoreInstrumentation`, `AddHttpClientInstrumentation`, `AddRuntimeInstrumentation`, and `AddOtlpExporter` — enables request rate, DB latency, and runtime GC/thread metrics.
- Added `OpenTelemetry.Instrumentation.Runtime 1.10.0` to `SmoothOperator.Infrastructure.csproj`.

**Frontend:**
- Extracted all Chart.js logic from `monitoring.ts` into new `MonitoringChartsComponent` (`monitoring-charts.component.ts/html/css`) — Chart.js now loads in a separate lazy chunk (208 kB) rather than the main bundle.
- Added `@defer (on idle)` block in `monitoring.html` — charts render after the browser becomes idle, keeping initial paint fast.
- Parent `monitoring.ts` reduced to data-fetching + signals; `chartData = signal<MonitoringChartData | null>(null)` passed as `@Input` to child.
- `ngAfterViewInit` (with `@ViewChild` + canvas refs) replaced by `ngOnInit` — no DOM dependency in parent.

## 2026-07-13 - Phase 9: Docker & Delivery Hardening

**Summary:** Hardened all Docker artifacts for production-grade security, non-root operation, and minimal attack surface. Both `smooth-operator-backend` and `smooth-operator-frontend` images build and pass health checks.

**Backend (`backend/Dockerfile`):**
- Non-root runtime: `USER $APP_UID` (uid 1654, pre-baked in `mcr.microsoft.com/dotnet/aspnet` base image).
- Health probe: installed `curl` (minimal, no recommends); `HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 CMD curl -fsS http://localhost:8080/health || exit 1`.
- Publish stage: added `-r linux-x64 --no-self-contained /p:PublishReadyToRun=true` for faster cold start; restore stage updated to match with `-r linux-x64 /p:PublishReadyToRun=true`.
- `ImplicitUsings>enable` + `Nullable>enable` added to all 4 `.csproj` files (Domain, Application, Infrastructure, Api) — fixed latent compile errors exposed when Docker rebuilds without incremental `obj/` cache.

**Frontend (`frontend/Dockerfile`, `frontend/nginx.conf`):**
- Base image: `nginxinc/nginx-unprivileged:1.27-alpine` (runs as `nginx` uid 101, listens on 8080).
- Security headers via `nginx.conf`: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy`, full `Content-Security-Policy` allowing `ws:/wss:` for Guacamole WebSocket; HSTS for non-localhost.
- Cache-control: `no-store` for `index.html`; `1y` immutable for content-hashed assets.
- Expanded gzip: `application/json`, `application/javascript`, `image/svg+xml`, `text/javascript`, `gzip_min_length 1024`.
- `HEALTHCHECK CMD wget -qO- http://localhost:8080/ || exit 1`.
- `entrypoint.sh`: `mkdir -p /usr/share/nginx/html/config` guard for `read_only: true` + tmpfs.

**docker-compose.yml:**
- `cap_drop: [ALL]` on both frontend and backend services.
- Frontend: `read_only: true` + `tmpfs: [/tmp, /usr/share/nginx/html/config]`.
- Backend: `tmpfs: [/tmp]` (`read_only: true` deferred to Phase 11 — requires Data Protection persistence first).
- Port: frontend now binds `4200:8080`.

**Build fixes:**
- `backend/.dockerignore`: changed bare `obj/bin/out/publish` → `**/obj **/bin **/out **/publish` — prevents Windows `project.assets.json` leaking into Linux Docker builds.
- `.env.example`: created to document all required env vars.

## 2025-07-25 - Phase 11: Security Pass

- **Docker secrets**: added `builder.Configuration.AddKeyPerFile("/run/secrets", optional: true)` in `Program.cs`; file `/run/secrets/Jwt__Key` maps to `Jwt:Key` config key.
- **Hardcoded JWT key cleared**: `appsettings.json` `Jwt:Key` is now `""` — must be supplied via env or secret.
- **Legacy flat env keys removed**: `ENCRYPTION_KEY`, `APP_URL`, `FRONTEND_URL` removed from `appsettings.json`; Options pattern bindings in place.
- **Data Protection**: `AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(config.DataProtection.KeysPath))` so antiforgery / cookie keys survive container restarts; `dp_keys` named volume added to `docker-compose.yml`; `DataProtection.KeysPath` config key added to `appsettings.json`.
- **CORS**: `AddCors("AllowConfiguredOrigins")` reading from `AppUrlsOptions.AllowedOrigins[]`; `UseCors` inserted between `UseRouting` and `UseOutputCache` in middleware pipeline; `AllowedOrigins` property added to `AppUrlsOptions`.
- **[AllowAnonymous] antipattern fixed**: `GetEffectiveVaults`, `UpdateMyProfile`, `DeleteMyAvatar` removed from admin-role-restricted `UsersController`; moved to new `UserProfileController` with `[Authorize]` (no role) at class level, same `/api/users` route prefix — preserving API contract.
- **docker-compose**: `read_only: true` added to backend service; `dp_keys:/data/protection-keys` volume mount added; `dp_keys:` declared in top-level `volumes:` block.
- **CI fixed**: `dependency-scan.yml` `dotnet restore/list` target changed from `backend/Backend.csproj` → `smooth-operator.sln`; `*.sln` added to `paths:` trigger.
- **secrets/ gitignored**: `secrets/` entry + `!secrets/.gitkeep` added to `.gitignore`; `secrets/.gitkeep` placeholder committed.
- **Output cache invalidation fix**: `SystemSettingsController.Update` now calls `cacheStore.EvictByTagAsync("system-settings")` after write; GET tagged with `"system-settings"` — prevents stale-read test failure.
- All 151 tests pass (142 integration + 9 architecture).


## 2025-07-25 - Phase 12: Final Cleanup & PR

Opened PR #50 to master: all 151 tests pass (142 integration + 9 architecture). README updated with Clean Architecture diagram and full Options-pattern env-var matrix. ADR-0007 documents Phase 11 security decisions. Branch: ac/refactor-testing-performance.

## 2025-07-25 - Azure Key Vault Integration

**Summary:** Added full Azure Key Vault integration: credentials can now be stored locally (as before) or referenced in Azure Key Vault via a registered secret provider.

**Backend (previously completed):**
- `SecretProvider` entity + `SecretStorageMode` / `SecretProviderType` enums.
- `ISecretProvider` / `ISecretProviderFactory` abstraction.
- `AzureKeyVaultSecretProvider` + `SecretProviderFactory` (Infrastructure).
- MediatR commands/queries for CRUD + test-connection on providers.
- Extended `CreateCredentialCommand` / `UpdateCredentialCommand` for push/link flows (plaintext never persisted when mode=External).
- `SecretProvidersController` (Owner/Admin only).
- `GuacamoleProxyService` resolves secrets via factory at connect time.
- EF Core migration `AddSecretProviders`.

**Frontend:**
- `SecretProvidersService` — CRUD + test + listSecrets.
- New `Settings → Key Vault` page (`secret-providers/`): list table, inline add/edit form, per-row test-connection button, isEnabled toggle (edit mode only).
- Route added: `settings/secret-providers` lazy-loaded; tab added to `settings.html`.
- `credentials.service.ts` extended: `SecretStorageMode`, new fields on `Credential`, updated payloads.
- `credentials.ts` / `credentials.html` updated: Storage Mode toggle (Local / Azure Key Vault), provider picker, Push-to-Vault vs Link-Existing sub-mode, lazy secret-name dropdown.

**Security:** clientSecret AES-encrypted at rest; never returned in API responses; plaintext never written to DB when storageMode=External; `secret.fetched` audit event never logs value.
