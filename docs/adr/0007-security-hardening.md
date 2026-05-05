# ADR 0007 — Security Hardening: Docker Secrets, Data Protection, CORS, [AllowAnonymous] Elimination

**Status:** Accepted  
**Date:** 2025-07-25

## Context

Several security issues were identified during a dedicated security pass (Phase 11):

1. **Hardcoded JWT signing key** — `Jwt:Key` was committed to `appsettings.json` as a plaintext string, leaking secrets via source control.
2. **No ASP.NET Data Protection persistence** — the default ephemeral key ring meant antiforgery tokens and protected cookies were invalidated on every container restart, causing unexpected logouts and replay-protection gaps.
3. **CORS misconfiguration** — no CORS middleware was registered; the app relied entirely on the reverse-proxy header stripping, which is environment-dependent.
4. **`[AllowAnonymous]` antipattern** — three self-service endpoints in `UsersController` (class-level `[Authorize(Roles = OwnerOrAdmin)]`) used `[AllowAnonymous]` plus a manual `User.IsAuthenticated` guard to bypass the class-level role restriction. This pattern is brittle and inverts the principle of default-deny.
5. **CI dependency scan targeting single project** — the `dependency-scan.yml` workflow targeted `backend/Backend.csproj`, which no longer exists after the Clean Architecture restructure.
6. **`secrets/` directory not gitignored** — nothing prevented accidental commit of local Docker secrets.
7. **Output cache not invalidated on writes** — GET endpoints with `[OutputCache]` returned stale data after PUT updates in the same test run.

## Decision

### 1. Docker secrets support via `AddKeyPerFile`

```csharp
builder.Configuration.AddKeyPerFile(directoryPath: "/run/secrets", optional: true);
```

File `/run/secrets/Jwt__Key` maps to config key `Jwt:Key` (double-underscore separator). This is added with highest priority so Docker secrets override `appsettings.json` and environment variables. `optional: true` means development without mounted secrets continues to work.

### 2. Data Protection key persistence

```csharp
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(appUrls.DataProtection.KeysPath))
    .SetApplicationName("smooth-operator");
```

A named Docker volume `dp_keys` is mounted at `/data/protection-keys` so the key ring persists across container restarts.

### 3. CORS from configuration

A named policy `AllowConfiguredOrigins` reads from `AppUrlsOptions.AllowedOrigins[]`. In Development, a wildcard fallback allows all origins to ease local development. `app.UseCors(...)` is inserted between `UseRouting()` and `UseAuthentication()`.

### 4. Self-service endpoints moved to `UserProfileController`

Three endpoints (`GetEffectiveVaults`, `UpdateMyProfile`, `DeleteMyAvatar`) were extracted from the admin-restricted `UsersController` into a new `UserProfileController` with only `[Authorize]` at class level (no role requirement). This restores the same URL paths (`/api/users/{id}/effective-vaults`, `/api/users/me/profile`, `/api/users/me/avatar`) while correctly requiring authentication — not bypassing it.

**Why not `[Authorize]` on the method?** ASP.NET Core stacks `[Authorize]` attributes; a method-level `[Authorize]` adds to, not overrides, the class-level role requirement. A separate controller is the only clean solution.

### 5. Output cache tag-based invalidation

GET endpoints with `[OutputCache]` now carry a tag (e.g., `"system-settings"`). Their corresponding PUT/POST handlers inject `IOutputCacheStore` and call `EvictByTagAsync(tag, ct)` after writing. This ensures reads following writes see fresh data within the same request sequence.

### 6. `secrets/` gitignored with placeholder

```gitignore
secrets/
!secrets/.gitkeep
```

`secrets/.gitkeep` is committed so the directory structure is preserved in the repository for documentation purposes, but all file contents under `secrets/` are ignored.

### 7. CI fixed to target solution file

`dependency-scan.yml` now uses `dotnet restore smooth-operator.sln` and `dotnet list smooth-operator.sln package --vulnerable`, ensuring all four projects in the solution are scanned.

## Consequences

- **Positive:** Secrets no longer live in source control. Container restarts do not invalidate user sessions. CORS is explicit and auditable. The `[AllowAnonymous]` antipattern is eliminated. CI dependency scan covers the whole solution.
- **Negative:** A `dp_keys` named Docker volume must be present for the backend container to start (handled in `docker-compose.yml`). Production deployments must supply `Jwt__Key` and `Encryption__Key` via environment variables or Docker secrets.
- **Neutral:** `read_only: true` is now set on the backend container; the only writable path is `/data/protection-keys` (the volume mount). The `/tmp` path is implicitly available via Docker's default tmpfs behaviour.
