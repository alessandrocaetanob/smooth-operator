---
sidebar_position: 7
---

# Observability

Smooth Operator ships a built-in, fully integrated observability stack based on **Prometheus**, **Loki**, **Grafana Tempo**, and **Grafana**. The entire stack is **optional** — you can bring your own monitoring tools and connect them to the published endpoints.

---

## Starting the Observability Stack

The observability services are grouped under a Docker Compose **profile** so they don't block the core application stack.

### Core stack only (no observability)

```bash
docker compose up -d
```

### Core stack + full observability

```bash
docker compose --profile observability up -d
```

This starts four additional containers:

| Service | URL | Purpose |
|---------|-----|---------|
| Grafana | http://localhost:3001 | Unified dashboards & alerting |
| Prometheus | http://localhost:9090 | Metrics scraping & storage |
| Loki | http://localhost:3100 | Log aggregation |
| Grafana Tempo | (internal) `tempo:4317` | Distributed trace storage |

> **Note:** The application backend continues to work without the observability stack. If Loki is offline, log lines are silently dropped. If Prometheus is offline, metrics are simply not scraped.

---

## Prometheus Metrics

The backend exposes a Prometheus-compatible metrics endpoint:

```
http://<host>:8080/metrics
```

### Scrape configuration

Add the following to your `prometheus.yml` to scrape the application:

```yaml
scrape_configs:
  - job_name: smooth-operator-backend
    static_configs:
      - targets: ['backend:8080']
    metrics_path: /metrics
    scrape_interval: 15s
```

### Available metrics

| Metric | Type | Description |
|--------|------|-------------|
| `smooth_operator_login_attempts_total{outcome}` | Counter | Login attempts labelled by `success` or `failure` |
| `smooth_operator_active_sessions` | Gauge | Currently open RDP/SSH sessions |
| `smooth_operator_connections_started_total` | Counter | Total connections ever opened |
| `smooth_operator_audit_events_total{action}` | Counter | Audit events per action type |
| `http_requests_total` | Counter | HTTP request count (from ASP.NET Core middleware) |
| `http_request_duration_seconds` | Histogram | HTTP response time histogram |

---

## OpenTelemetry Traces (OTLP)

The backend exports distributed traces using the **OpenTelemetry Protocol (OTLP)** over gRPC. When the built-in observability stack is running, traces are delivered to **Grafana Tempo**.

### OTLP gRPC endpoint

```
grpc://tempo:4317     (internal Docker network)
grpc://localhost:4317 (host machine)
```

### Connecting a custom OpenTelemetry Collector

Point your collector to the same endpoint, or configure the `Otel:Endpoint` environment variable on the backend to redirect traces to your own collector:

```yaml
# docker-compose.override.yml
services:
  backend:
    environment:
      Otel__Endpoint: "http://my-collector:4317"
```

Example OpenTelemetry Collector pipeline:

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: "0.0.0.0:4317"

exporters:
  otlp/jaeger:
    endpoint: "jaeger:4317"
    tls:
      insecure: true

service:
  pipelines:
    traces:
      receivers: [otlp]
      exporters: [otlp/jaeger]
```

### Service name

All traces are tagged with `service.name = "smooth-operator-backend"`. Use this label to filter traces in your tracing UI.

---

## Loki Logs

Application logs are shipped to Loki in **CLEF (Compact Log Event Format)** using the `Serilog.Sinks.Grafana.Loki` sink.

### Loki push endpoint

```
http://loki:3100      (internal Docker network)
http://localhost:3100  (host machine)
```

### Log labels

Every log line carries these Loki labels:

| Label | Example values |
|-------|---------------|
| `app` | `smooth-operator` |
| `level` | `information`, `warning`, `error` |
| `EnvironmentName` | `Production`, `Development` |

### Useful LogQL queries

**All application logs:**
```logql
{app="smooth-operator"}
```

**Audit events only:**
```logql
{app="smooth-operator"} | json | action != ""
```

**Failed login attempts:**
```logql
{app="smooth-operator"} | json | action = `user.login_failed`
```

**Errors and warnings:**
```logql
{app="smooth-operator", level=~"error|warning"}
```

**Specific correlation ID (request tracing):**
```logql
{app="smooth-operator"} | json | CorrelationId = `<your-correlation-id>`
```

### Connecting a custom log aggregator

To ship logs to your own Loki-compatible endpoint, set the `Serilog__WriteTo__1__Args__uri` environment variable:

```yaml
# docker-compose.override.yml
services:
  backend:
    environment:
      Serilog__WriteTo__1__Args__uri: "http://my-loki:3100"
```

Or to use a completely different sink (e.g., Elasticsearch, Splunk), replace the Serilog configuration in `appsettings.json`.

---

## Grafana Dashboards

When the observability stack is running, Grafana is pre-provisioned with **four dashboards** accessible at http://localhost:3001.

| Dashboard | UID | Description |
|-----------|-----|-------------|
| ASP.NET Core Overview | `aspnetcore-overview` | HTTP request rate, latency (p50/p95), error rates, active sessions |
| Authentication & Security | `auth-security` | Login attempts, failure rate, failed login log stream |
| Active Sessions | `active-sessions` | Live RDP/SSH session gauge and connection history |
| Audit Events | `audit-events` | Audit event rate, success/failure breakdown, event log stream |

### Default credentials

The default Grafana login is `admin / admin`. **Change this immediately** in production by setting the `GF_SECURITY_ADMIN_PASSWORD` environment variable in `docker-compose.yml` before first start.

### Data sources

Three data sources are auto-provisioned:

| Name | UID | Type |
|------|-----|------|
| Prometheus | `Prometheus` | prometheus |
| Loki | `Loki` | loki |
| Tempo | `Tempo` | tempo |

---

## Integrations Settings Page

The application includes a built-in **Integrations** page at **Settings → Integrations** that surfaces all endpoint URLs, scrape configs, and LogQL examples for easy copy-paste. No need to memorise endpoints.

---

## Bringing Your Own Stack (BYOS)

You don't need the built-in observability stack. The application exposes standard-protocol endpoints that work with any compatible tool:

| Protocol | Endpoint | Compatible tools |
|----------|---------|-----------------|
| Prometheus scrape | `:8080/metrics` | Prometheus, Datadog Agent, VictoriaMetrics, Grafana Cloud |
| OTLP gRPC | `:4317` (Tempo) | Jaeger, Zipkin, Datadog, New Relic, Honeycomb, any OTel Collector |
| Loki push | `:3100` | Grafana Cloud, any Loki-compatible aggregator |

Start the core stack without observability, point your own tools at these endpoints, and everything works.
