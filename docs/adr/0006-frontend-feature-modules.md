# ADR 0006 — Frontend Feature Module Architecture

**Status:** Accepted  
**Date:** 2026-07-13

## Context

All Angular pages were flat under `src/app/pages/`, all services under `src/app/services/`. There were no lazy-loaded routes, no feature isolation, no smart/dumb component split. State was managed ad-hoc with `signal()` and `BehaviorSubject` in services that were all eagerly loaded at startup.

## Decision

Adopt a **feature module** structure with **smart/dumb component separation** and **NgRx Signal Stores**:

```
frontend/src/app/
├── core/            # Singletons (auth, interceptors, guards, initializers)
├── shared/          # Reusable dumb components, pipes, directives
└── features/        # One folder per domain feature, lazy-loaded
    └── <feature>/
        ├── data/    # NgRx Signal Store + HTTP service
        ├── ui/      # Dumb presentational components (Input/Output only)
        ├── pages/   # Smart components (inject store/services)
        └── <feature>.routes.ts
```

### NgRx Signal Stores
- One `SignalStore` per feature in `data/`; replaces ad-hoc `signal()` + `BehaviorSubject` patterns
- Store actions are plain methods; effects are async methods calling HTTP service
- Components declare `inject(FeatureStore)` — no `@Input()` for data that belongs to the store

### Lazy Loading
- `app.routes.ts` uses `loadChildren(() => import('./features/<f>/<f>.routes'))` for every feature
- Reduces initial bundle size; each feature chunk is ~15–50 KB compressed

## Consequences

**Benefits:**
- Each feature is independently testable: stub the Signal Store, render the component.
- Lazy loading reduces time-to-interactive (initial bundle size shrinks).
- Clear code navigation: all code for "connections" is in `features/connections/`.
- Dumb components in `ui/` have no dependencies — trivially unit-tested.

**Trade-offs:**
- Migration effort: each `pages/<name>` folder needs to become `features/<name>/pages/`.
- NgRx Signal Stores are newer (Angular 18+) — team needs to understand the pattern.
- Lazy routing adds one async boundary per navigation; imperceptible with preloading strategies.
