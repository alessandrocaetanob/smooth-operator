# Load tests

[k6](https://k6.io) smoke test for the Smooth Operator API hot paths.

## Prerequisites

- A running stack: `docker compose up --build` (backend reachable at `http://localhost:5000`).
- k6 installed: `winget install k6` / `brew install k6` / [other options](https://grafana.com/docs/k6/latest/set-up/install-k6/).
- A valid user account. The script defaults to `admin@example.com`; override with env vars.

## Run

```bash
# Defaults: BASE_URL=http://localhost:5000, SO_EMAIL=admin@example.com
k6 run load-tests/smoke.js

# Override target and credentials
BASE_URL=http://localhost:5000 \
SO_EMAIL=you@example.com \
SO_PASSWORD='your-password' \
  k6 run load-tests/smoke.js
```

## What it covers

| Step | Endpoint | Why |
|------|----------|-----|
| 1 | `GET /api/auth/setup-status` | Public app-initializer call on every page load |
| 2 | `POST /api/auth/login` | Credential auth; sets the httpOnly cookie |
| 3 | `GET /api/connections` | `GetConnectionsQuery` + EF eager loads |

## Thresholds

The run fails if the error rate exceeds 1% or the 95th-percentile request
duration exceeds 500 ms. Tune `options.thresholds` in `smoke.js` as the
performance baseline shifts.

## Comparing before/after

Capture the summary (`k6 run --summary-export=baseline.json load-tests/smoke.js`)
before a change, then again after, and diff `http_req_duration` p95/throughput.
Cross-reference with the `smooth_operator_mediatr_request_duration_seconds`
histogram in Prometheus/Grafana for per-handler attribution.
