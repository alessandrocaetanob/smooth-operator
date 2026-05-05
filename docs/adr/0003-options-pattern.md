# ADR 0003 — Options Pattern for Configuration

**Status:** Accepted  
**Date:** 2026-07-13

## Context

The original code read configuration via raw `IConfiguration` indexers scattered across 15+ sites:
```csharp
configuration["Guacd:Host"]
int.Parse(configuration["Guacd:Port"] ?? "4822")
Environment.GetEnvironmentVariable("ENCRYPTION_KEY")
```
Problems: no type safety, no validation at startup, default values hidden in service constructors, no IDE navigation from "where is this key used?".

## Decision

Use the **Options pattern** (`IOptions<T>` / `IOptionsMonitor<T>`) with strongly-typed classes in `SmoothOperator.Application.Options/`:

| Class | Section | Key secrets |
|-------|---------|-------------|
| `EncryptionOptions` | `Encryption` | `Key` (64-char hex, validated) |
| `JwtOptions` | `Jwt` | `Key`, `Issuer`, `Audience`, `ExpireMinutes` |
| `GuacdOptions` | `Guacd` | `Host`, `Port` (range 1–65535) |
| `SmtpOptions` | `Smtp` | `Host`, `Port`, `User`, `Password`, `From` |
| `AppUrlsOptions` | `AppUrls` | `App`, `Frontend`, `AllowedOrigins[]` |
| `OtelOptions` | `Otel` | `Endpoint` |
| `RateLimitOptions` | `RateLimit` | `GlobalRpm`, `AuthRpm`, `AuthRpd` |

All options are registered with `ValidateDataAnnotations().ValidateOnStart()` so a misconfigured deployment fails immediately at startup rather than at the first request.

Environment variables override `appsettings.json` using the `__` double-underscore convention (e.g., `Encryption__Key`).

## Consequences

**Benefits:**
- Fail-fast: misconfiguration is caught at startup.
- Type-safe: no more `int.Parse`, no more `?? "default"` noise.
- Testable: options classes are plain C# objects, trivially unit-tested.
- IDE navigation: Go To Definition on `options.Key` reaches the class.

**Trade-offs:**
- More boilerplate: one class and one `services.AddOptions<T>()` call per section.
- Secrets management: keys like `Encryption__Key` and `Jwt__Key` should come from Docker secrets or a secrets manager in production (see Phase 11).
