# Architecture Overview

Smooth Operator is a self-hosted remote access management platform. Users authenticate, manage connection credentials, and launch Guacamole-proxied sessions (RDP, SSH, VNC) from a browser — all without exposing ports directly.

---

## C4 Level 1 — System Context

```mermaid
%%{init: {
  'theme': 'base',
  'themeVariables': {
    'mainBkg': '#ffffff',
    'c4ShapeFill': '#ffffff',
    'c4ShapeBorderColor': '#000000',
    'c4ShapeTextColor': '#000000',
    'c4LineColor': '#000000',
    'c4BoundaryBorderColor': '#000000',
    'c4BoundaryTextColor': '#000000',
    'fontSize': '16px',
    'fontFamily': 'Inter, system-ui, sans-serif'
  }
}}%%
C4Context
  title System Context — Smooth Operator

  Person(user, "Operator User", "Browser-based access to remote hosts")
  Person(admin, "Administrator", "Manages users, groups, SSO, and audit logs")

  System(smoothOp, "Smooth Operator", "Self-hosted remote access management portal (Angular + .NET + Guacamole)")

  System_Ext(guacd, "guacd", "Apache Guacamole daemon — proxies RDP/SSH/VNC protocols over WebSocket")
  System_Ext(ldapSso, "IdP / LDAP", "SSO or LDAP identity provider (optional)")
  System_Ext(smtp, "SMTP Server", "Email delivery for invites and notifications")
  System_Ext(otel, "OpenTelemetry Collector", "Receives traces and metrics from the API")

  Rel(user, smoothOp, "Launches remote sessions, manages vault")
  Rel(admin, smoothOp, "Configures SSO, SMTP, audit logs, rate limits")
  Rel(smoothOp, guacd, "Proxies WebSocket tunnel")
  Rel(smoothOp, ldapSso, "Authenticates users via OIDC/LDAP")
  Rel(smoothOp, smtp, "Sends transactional emails")
  Rel(smoothOp, otel, "Exports traces and metrics (OTLP/gRPC)")
```

---

## C4 Level 2 — Container Diagram

```mermaid
%%{init: {
  'theme': 'base',
  'themeVariables': {
    'mainBkg': '#ffffff',
    'c4ShapeFill': '#ffffff',
    'c4ShapeBorderColor': '#000000',
    'c4ShapeTextColor': '#000000',
    'c4LineColor': '#000000',
    'c4BoundaryBorderColor': '#000000',
    'c4BoundaryTextColor': '#000000',
    'fontSize': '16px',
    'fontFamily': 'Inter, system-ui, sans-serif'
  }
}}%%
C4Container
  title Container Diagram — Smooth Operator

  Person(user, "User / Admin", "Browser")

  Container(nginx, "nginx (Frontend)", "nginx:alpine", "Serves Angular SPA, terminates HTTP, proxies /api to backend")
  Container(api, "API Server", ".NET 10 ASP.NET Core", "REST API, WebSocket tunnel, MediatR CQRS, JWT auth")
  ContainerDb(postgres, "PostgreSQL 17", "Database", "Connections, users, groups, credentials, audit logs, SSO settings")
  ContainerDb(redis, "Redis 7", "Cache / Session", "Guacamole session tokens, rate-limit counters, distributed lock")

  Container_Ext(guacd, "guacd", "C daemon", "Guacamole protocol proxy")

  Rel(user, nginx, "HTTPS", "443")
  Rel(nginx, api, "HTTP proxy", "/api/*")
  Rel(nginx, api, "WS proxy", "/websocket-tunnel")
  Rel(api, postgres, "TCP", "5432")
  Rel(api, redis, "TCP", "6379")
  Rel(api, guacd, "TCP", "4822")
```

---

## C4 Level 3 — Backend Component Diagram

```mermaid
%%{init: {
  'theme': 'base',
  'themeVariables': {
    'mainBkg': '#ffffff',
    'c4ShapeFill': '#ffffff',
    'c4ShapeBorderColor': '#000000',
    'c4ShapeTextColor': '#000000',
    'c4LineColor': '#000000',
    'c4BoundaryBorderColor': '#000000',
    'c4BoundaryTextColor': '#000000',
    'fontSize': '16px',
    'fontFamily': 'Inter, system-ui, sans-serif'
  }
}}%%
C4Component
  title Backend Component — SmoothOperator.Api

  Container_Boundary(api, "SmoothOperator.Api (.NET)") {
    Component(controllers, "Controllers", "ASP.NET Controllers", "Thin: validate JWT, call mediator.Send()")
    Component(middleware, "Middleware", "ASP.NET Middleware", "CorrelationId, SecurityHeaders, RateLimiting, ExceptionHandler")
    Component(program, "Program.cs", "Composition Root", "DI registrations, OpenTelemetry, Serilog, Health checks")
  }

  Container_Boundary(app, "SmoothOperator.Application") {
    Component(handlers, "CQRS Handlers", "MediatR IRequestHandler", "All business logic; one handler per command/query")
    Component(validators, "FluentValidation", "AbstractValidator<T>", "Input validation in pipeline before handler runs")
    Component(pipeline, "Pipeline Behaviors", "IPipelineBehavior", "Logging, validation, exception handling")
    Component(ports, "Ports (Interfaces)", "C# Interfaces", "IEncryptionService, IEmailService, IAuditService, ITokenService")
    Component(options, "Options Classes", "IOptions<T>", "JwtOptions, GuacdOptions, SmtpOptions, EncryptionOptions, ...")
  }

  Container_Boundary(infra, "SmoothOperator.Infrastructure") {
    Component(efcore, "AppDbContext", "EF Core 10", "PostgreSQL persistence, migrations, entity configurations")
    Component(redisAdapter, "Redis Adapters", "StackExchange.Redis", "Session token store, rate-limit counters")
    Component(email, "MailKit Adapter", "MailKit", "SMTP email delivery")
    Component(encryption, "Encryption Service", "AES-256-GCM", "Credential encryption/decryption")
    Component(sso, "SSO Adapters", "OpenIddict / LDAP", "OIDC token exchange, LDAP bind")
    Component(guacProxy, "Guacamole Proxy", "TCP↔WebSocket", "Guacamole handshake, session pump, metrics")
  }

  Container_Boundary(domain, "SmoothOperator.Domain") {
    Component(entities, "Entities", "C# Classes", "Connection, User, Group, Credential, AuditLog, ...")
    Component(valueObjects, "Value Objects", "C# Records", "Immutable types with domain validation")
  }

  Rel(controllers, handlers, "mediator.Send()")
  Rel(handlers, ports, "calls interfaces")
  Rel(handlers, entities, "operates on domain objects")
  Rel(infra, ports, "implements")
  Rel(efcore, entities, "persists/reads")
  Rel(guacProxy, redisAdapter, "stores session tokens")
  Rel(middleware, program, "registered by")
```

---

## Backend Layer Dependencies

```mermaid
%%{init: {
  'theme': 'base',
  'themeVariables': {
    'primaryColor': '#ffffff',
    'primaryBorderColor': '#000000',
    'primaryTextColor': '#000000',
    'lineColor': '#000000',
    'fontSize': '16px',
    'fontFamily': 'Inter, system-ui, sans-serif',
    'mainBkg': '#ffffff'
  }
}}%%
graph TD
  Api["SmoothOperator.Api"] --> Application["SmoothOperator.Application"]
  Api --> Infrastructure["SmoothOperator.Infrastructure"]
  Infrastructure --> Application
  Infrastructure --> Domain["SmoothOperator.Domain"]
  Application --> Domain
```

> Dependency rules are enforced by `SmoothOperator.ArchitectureTests` (NetArchTest) — any violation fails CI.

---

## Frontend Component Architecture

```mermaid
%%{init: {
  'theme': 'base',
  'themeVariables': {
    'primaryColor': '#ffffff',
    'primaryBorderColor': '#000000',
    'primaryTextColor': '#000000',
    'lineColor': '#000000',
    'fontSize': '16px',
    'fontFamily': 'Inter, system-ui, sans-serif',
    'mainBkg': '#ffffff',
    'clusterBkg': '#f8f9fa',
    'clusterBorder': '#000000'
  }
}}%%
graph TD
  AppRoutes["app.routes.ts (lazy)"] --> CoreModule["core/\n(singletons: auth, interceptors, guards)"]
  AppRoutes --> SharedModule["shared/\n(dumb components, pipes, directives)"]
  AppRoutes --> Features["features/\n(one folder per domain)"]

  subgraph Feature [features/connections/ example]
    Pages["pages/ (smart, inject store)"] --> Store["data/SignalStore\n+ HTTP service"]
    Pages --> UI["ui/ (dumb, Input/Output only)"]
  end

  Features --> Feature
```

---

## Request Flow (typical API call)

```mermaid
%%{init: {
  'theme': 'base',
  'themeVariables': {
    'actorBkg': '#ffffff',
    'actorBorder': '#000000',
    'actorTextColor': '#000000',
    'actorLineColor': '#000000',
    'signalColor': '#000000',
    'signalTextColor': '#000000',
    'labelBoxBkgColor': '#ffffff',
    'labelBoxBorderColor': '#000000',
    'labelTextColor': '#000000',
    'loopTextColor': '#000000',
    'noteBkgColor': '#ffffff',
    'noteBorderColor': '#000000',
    'noteTextColor': '#000000',
    'mainBkg': '#ffffff',
    'fontSize': '16px',
    'fontFamily': 'Inter, system-ui, sans-serif'
  }
}}%%
sequenceDiagram
  participant Browser
  participant nginx
  participant Controller
  participant MediatR
  participant ValidationBehavior
  participant Handler
  participant EFCore
  participant PostgreSQL

  Browser->>nginx: GET /api/connections (JWT)
  nginx->>Controller: proxy
  Controller->>MediatR: Send(GetConnectionsQuery)
  MediatR->>ValidationBehavior: validate
  ValidationBehavior->>Handler: pass
  Handler->>EFCore: context.Connections.AsNoTracking()...
  EFCore->>PostgreSQL: SELECT ...
  PostgreSQL-->>EFCore: rows
  EFCore-->>Handler: List<ConnectionDto>
  Handler-->>Controller: result
  Controller-->>Browser: 200 OK (JSON)
```
