# ADR 0004 — Frontend Runtime Configuration

**Status:** Accepted  
**Date:** 2026-07-13

## Context

The Angular frontend had no environment files; all non-secret URLs were hardcoded:
- `helpUrl = 'http://localhost:3000'` in `SideNavBarComponent`
- `href="http://localhost:3000"` in `authentication.html`

Using Angular's `environment.ts` would require rebuilding the image for each deployment environment, violating the "build once, deploy anywhere" principle.

## Decision

Serve a **runtime config JSON file** (`/config/config.json`) from the nginx container and load it via an `APP_INITIALIZER` before Angular bootstraps:

1. `RuntimeConfigService` fetches `/config/config.json` with native `fetch` in an `APP_INITIALIZER`.
2. The `public/config/config.json` file ships with dev defaults (`localhost:3000`).
3. In production Docker containers, `entrypoint.sh` generates `config.json` from environment variables (`APP_HELP_URL`, `APP_DOCS_URL`, `APP_FEATURE_FLAGS`) using `envsubst` before `nginx` starts.
4. On network failure the service falls back to compile-time defaults — Angular bootstrap is never blocked.

```json
{
  "helpUrl": "https://docs.your-domain.com",
  "docsUrl": "https://docs.your-domain.com",
  "featureFlags": {}
}
```

## Consequences

**Benefits:**
- Same Docker image works across dev, staging, and production — no rebuild needed.
- Non-secret config (URLs, feature flags) can be changed without redeployment.
- Consistent pattern: all components read from `RuntimeConfigService`.

**Trade-offs:**
- One extra HTTP request at bootstrap time (~5 ms on LAN, negligible).
- Requires `entrypoint.sh` in the Dockerfile; image must include `envsubst` (available in `nginx:alpine`).
- Feature flags are strings in JSON — no type safety without a TypeScript interface update.
