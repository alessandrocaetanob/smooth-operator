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


