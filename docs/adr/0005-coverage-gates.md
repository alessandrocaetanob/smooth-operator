# ADR 0005 — Coverage Gates in CI

**Status:** Accepted  
**Date:** 2026-07-13

## Context

Tests existed but CI never failed when coverage dropped. There was no mechanism to prevent regressions in test coverage. The codebase had sparse frontend tests (4 spec files for 19 page folders) and no branch-coverage enforcement on the backend.

## Decision

Enforce minimum coverage thresholds in CI:

### Backend (Coverlet + Cobertura)
- **Line coverage ≥ 80%** and **Branch coverage ≥ 80%** (total across all src assemblies)
- Enforced via: `dotnet test /p:Threshold=80 /p:ThresholdType=line,branch /p:ThresholdStat=total`
- HTML report uploaded as a CI artifact via `reportgenerator`

### Frontend (Vitest + @vitest/coverage-v8)
- **Lines ≥ 70%**, **Branches ≥ 70%**, **Functions ≥ 70%**, **Statements ≥ 70%**
- Enforced via `coverageThresholds` in `angular.json` or `vitest.config.ts`
- Currently at ~39.5% lines / 44.3% branches — gate is recorded but not yet blocking; enabled once tests are added in Phase 8

### Architecture tests (NetArchTest)
- Separate `SmoothOperator.ArchitectureTests` project with 9 dependency-rule assertions
- Fails CI if any layer dependency is violated

## Consequences

**Benefits:**
- Prevents coverage regressions from being merged silently.
- Architecture tests prevent accidental cross-layer shortcuts.
- Mutation testing (Stryker.NET) available for manual quarterly runs.

**Trade-offs:**
- Initial test-writing investment required to reach thresholds.
- 80% backend gate may need to be raised incrementally (start gate disabled, enable in Phase 8).
