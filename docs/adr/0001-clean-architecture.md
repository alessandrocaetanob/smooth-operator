# ADR 0001 — Clean Architecture for Backend

**Status:** Accepted  
**Date:** 2026-07-13

## Context

The original `Backend.csproj` was a single-project monolith mixing controllers, EF entities, EF context, infrastructure adapters (Redis, SMTP, encryption), and business logic in one assembly. Files grew large (`GuacamoleProxyService.cs` 728 LOC, `ConnectionsController.cs` 513 LOC). There were no domain or application layers — the API layer directly contained all business rules, making unit testing nearly impossible without spinning up the full ASP.NET host.

## Decision

Adopt **Clean Architecture** (inspired by Robert C. Martin) with four projects:

| Layer | Project | Allowed dependencies |
|-------|---------|---------------------|
| Domain | `SmoothOperator.Domain` | None |
| Application | `SmoothOperator.Application` | Domain |
| Infrastructure | `SmoothOperator.Infrastructure` | Application, Domain |
| API | `SmoothOperator.Api` | Application, Infrastructure |

Architecture dependency rules are enforced at CI time by `SmoothOperator.ArchitectureTests` using **NetArchTest.Rules** (see ADR 0005).

## Consequences

**Benefits:**
- Domain and Application layers are testable without EF Core, HTTP, or any infrastructure.
- Infrastructure implementations can be swapped (e.g., replace Redis with in-memory for tests).
- Controllers become thin dispatchers; all logic lives in handlers (see ADR 0002).

**Trade-offs:**
- More projects to manage; more `ProjectReference` entries in the solution.
- Some mappings and DTOs require Mapster boilerplate (mitigated by source generators).
