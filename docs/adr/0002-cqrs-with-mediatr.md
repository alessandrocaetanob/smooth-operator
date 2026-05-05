# ADR 0002 — CQRS with MediatR

**Status:** Accepted  
**Date:** 2026-07-13

## Context

Controllers originally contained business logic directly (validation, EF queries, mapping, email dispatch). Fat controllers meant:
- Difficult unit tests (had to mock `DbContext`, `IHttpContextAccessor`, encryption, mail, etc.)
- No clear seam for cross-cutting concerns (logging, validation)
- Hard to find "where does X happen?"

## Decision

Adopt **CQRS** (Command Query Responsibility Segregation) via **MediatR 12.x**:

- Every controller action sends a single `IRequest<T>` (command or query) via `mediator.Send(...)`.
- Commands mutate state; Queries read state. Both live in `SmoothOperator.Application.Features.<FeatureName>/`.
- A **MediatR pipeline** applies cross-cutting concerns in order:
  1. `UnhandledExceptionBehavior` — catch + log exceptions, return `ProblemDetails`
  2. `LoggingBehavior` — structured logs with request/response timing
  3. `ValidationBehavior` — FluentValidation (fail-fast before handler runs)

## Consequences

**Benefits:**
- Controllers are ~20–80 LOC thin dispatchers; no business logic.
- Application handlers are pure functions (no HTTP concerns) — easily unit-testable.
- Pipeline behaviors centralise logging, validation, and exception handling.
- Feature isolation: all code for one feature lives in one folder.

**Trade-offs:**
- Indirection: a request traverses 3 behaviors before reaching the handler.
- MediatR adds ~0.5 ms overhead per request (acceptable given network latency).
- Requires discipline to not put logic back in controllers.
