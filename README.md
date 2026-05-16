# Smooth Operator

Smooth Operator is a cloud-native, clientless remote access vault. It gives teams secure, browser-based access to SSH, RDP, and VNC servers—without VPN clients, exposed ports, or direct credential sharing.

End users never see actual credentials. Admins control exactly who can access what, through granular role-based permissions and vault assignments.

> Full documentation → [http://localhost:3000](http://localhost:3000) (start the docs container with `docker compose up docs`)

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=alessandrocaetanob_smooth-operator&metric=alert_status&token=09830f0b5cf4a5c457100e56455064855fc33559)](https://sonarcloud.io/summary/new_code?id=alessandrocaetanob_smooth-operator)
[![codecov](https://codecov.io/gh/alessandrocaetanob/smooth-operator/graph/badge.svg?token=6WSJBQ1HU6)](https://codecov.io/gh/alessandrocaetanob/smooth-operator)

[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=alessandrocaetanob_smooth-operator&metric=security_rating&token=09830f0b5cf4a5c457100e56455064855fc33559)](https://sonarcloud.io/summary/new_code?id=alessandrocaetanob_smooth-operator)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=alessandrocaetanob_smooth-operator&metric=reliability_rating&token=09830f0b5cf4a5c457100e56455064855fc33559)](https://sonarcloud.io/summary/new_code?id=alessandrocaetanob_smooth-operator)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=alessandrocaetanob_smooth-operator&metric=sqale_rating&token=09830f0b5cf4a5c457100e56455064855fc33559)](https://sonarcloud.io/summary/new_code?id=alessandrocaetanob_smooth-operator)

[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=alessandrocaetanob_smooth-operator&metric=vulnerabilities&token=09830f0b5cf4a5c457100e56455064855fc33559)](https://sonarcloud.io/summary/new_code?id=alessandrocaetanob_smooth-operator)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=alessandrocaetanob_smooth-operator&metric=bugs&token=09830f0b5cf4a5c457100e56455064855fc33559)](https://sonarcloud.io/summary/new_code?id=alessandrocaetanob_smooth-operator)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=alessandrocaetanob_smooth-operator&metric=code_smells&token=09830f0b5cf4a5c457100e56455064855fc33559)](https://sonarcloud.io/summary/new_code?id=alessandrocaetanob_smooth-operator)
[![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=alessandrocaetanob_smooth-operator&metric=sqale_index&token=09830f0b5cf4a5c457100e56455064855fc33559)](https://sonarcloud.io/summary/new_code?id=alessandrocaetanob_smooth-operator)
[![Duplicated Lines (%)](https://sonarcloud.io/api/project_badges/measure?project=alessandrocaetanob_smooth-operator&metric=duplicated_lines_density&token=09830f0b5cf4a5c457100e56455064855fc33559)](https://sonarcloud.io/summary/new_code?id=alessandrocaetanob_smooth-operator)

---

## Architecture

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
    'edgeLabelBackground': '#ffffff',
    'clusterBkg': '#f8f9fa',
    'clusterBorder': '#000000'
  }
}}%%
graph TD
    Browser([Browser]) -->|HTTPS + WebSocket| Frontend

    subgraph Docker Stack
        Frontend["Angular 21\nnginx · :4200"]
        Backend[".NET 10 API\n:5000"]
        DB[("PostgreSQL 18\n:5432")]
        Cache[("Redis 7\n:6379")]
        Guacd["guacd 1.6\nApache Guacamole\n:4822"]
        Docs["Docusaurus\nDocs Site · :3000"]
        
        subgraph Observability
            Loki["Loki (Logs)"]
            Prom["Prometheus (Metrics)"]
            Tempo["Tempo (Traces)"]
        end
    end

    Frontend -->|REST + WSS| Backend
    Backend -->|EF Core / Npgsql| DB
    Backend -->|StackExchange.Redis| Cache
    Backend -->|TCP 4822| Guacd
    Backend -->|OTLP| Tempo
    Backend -->|Metrics| Prom
    Backend -->|Logs| Loki
    Guacd -->|SSH / RDP / VNC| Targets[Target Servers]
```

### Component Breakdown

| Component | Technology | Role |
|-----------|-----------|------|
| **Frontend** | Angular 21, Tailwind CSS 4, guacamole-common-js | SPA served via nginx; lazy-loaded feature modules, NgRx Signal Stores, runtime config |
| **Backend** | .NET 10, ASP.NET Core, EF Core 10 | Clean Architecture (Domain/Application/Infrastructure/Api); CQRS via MediatR; REST API + WebSocket tunnel |
| **Database** | PostgreSQL 18.4 | Users, vaults, connections, credentials, groups, audit logs |
| **Cache** | Redis 7 | Rate limiting, session state |
| **Connection Engine** | Apache guacd 1.6 | Translates RDP/SSH/VNC to Guacamole protocol over WebSocket |
| **Observability** | Prometheus, Loki, Tempo | Metrics, centralized logging, and distributed tracing |
| **Docs** | Docusaurus 3 | User guide, admin guide, and API reference |

### Backend Architecture (Clean Architecture + CQRS)

```
backend/
├── src/
│   ├── SmoothOperator.Domain/         # Entities, value objects. Zero dependencies.
│   ├── SmoothOperator.Application/    # MediatR commands/queries/handlers, FluentValidation,
│   │                                  # DTOs, Options classes, ports (interfaces).
│   ├── SmoothOperator.Infrastructure/ # EF Core, Redis, MailKit, encryption, SSO adapters.
│   └── SmoothOperator.Api/            # Thin controllers (single MediatR.Send per action),
│                                      # DI composition root, middleware, Program.cs.
└── tests/
    ├── SmoothOperator.Api.Tests/      # WebApplicationFactory integration tests.
    └── SmoothOperator.ArchitectureTests/ # NetArchTest layer-boundary enforcement.
```

Dependency rule: `Api → Application → Domain`; `Infrastructure → Application, Domain`.

---

## Remote Session Flow

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
    actor User as User
    participant FE as Angular Frontend
    participant API as .NET API
    participant DB as PostgreSQL
    participant G as guacd
    participant Srv as Target Server

    User->>FE: Click "Connect"
    FE->>API: POST /api/guacamole/ticket/{id}
    API->>DB: Check permission + fetch params
    DB-->>API: OK + encrypted credentials
    API-->>FE: One-time ticket token
    FE->>API: WS /api/guacamole/connect/{id}?ticket=…
    API->>G: Guacamole handshake (host, port, creds)
    G->>Srv: SSH / RDP / VNC protocol
    Srv-->>G: Remote desktop stream
    G-->>API: Guacamole binary frames
    API-->>FE: WebSocket relay
    FE->>User: Live HTML5 Canvas session
```

---

## Data Model

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
    'edgeLabelBackground': '#ffffff'
  }
}}%%
erDiagram
    USER {
        uuid    id
        string  email
        string  displayName
        string  role
        bool    isActive
    }
    USER_GROUP {
        uuid    id
        string  name
    }
    VAULT {
        uuid    id
        string  name
    }
    CONNECTION {
        uuid    id
        string  name
        string  protocol
        string  host
        int     port
    }
    CREDENTIAL {
        uuid    id
        string  name
        string  type
    }
    KNOWN_HOST {
        uuid    id
        string  hostname
        string  fingerprint
    }
    AUDIT_LOG {
        uuid      id
        string    action
        string    resourceType
        timestamp createdAt
    }

    USER        }o--o{  USER_GROUP  : "member of"
    USER_GROUP  }o--o{  VAULT       : "has access to"
    USER        }o--o{  VAULT       : "directly assigned"
    VAULT       ||--o{  CONNECTION  : "contains"
    CONNECTION  }o--o|  CREDENTIAL  : "uses"
    CONNECTION  }o--o|  KNOWN_HOST  : "validates"
    USER        ||--o{  AUDIT_LOG   : "generates"
```

---

## Features

### Security & Access Control
- **Role-Based Access Control (RBAC)** — four built-in roles: `Owner`, `Admin`, `TeamAdmin`, `User`
- **Vault-based isolation** — users only see connections in vaults they're assigned to, never raw credentials
- **Invite-only registration** — no public self-registration; admins send email invites
- **Rate limiting** — fixed-window limiter on auth endpoints (5 req/min per IP)

### Authentication
- **Local auth** — username/password with BCrypt hashing + HS256 JWT
- **SSO via OIDC** — plug in any OpenID Connect provider (Azure AD, Okta, Auth0, …)
- **SSO via SAML 2.0** — enterprise identity federation
- **Forgot-password flow** — email-based password reset (requires SMTP)

### Remote Sessions
- **RDP, SSH, VNC** — all three protocols via Apache Guacamole
- **Browser-native** — zero client software; runs on any modern browser
- **HTML5 Canvas rendering** — full keyboard/mouse capture via `guacamole-common-js`
- **Clipboard sharing** — bidirectional clipboard between browser and remote session
- **SSH key pair generation** — generate and store SSH keys directly in the vault

### Administration
- **User & group management** — create groups, assign members, bulk-grant vault access
- **Known hosts** — store and verify SSH host fingerprints
- **SMTP configuration** — connect any SMTP server for invite/reset emails; test with one click
- **Audit logs** — complete action history with IP addresses, exportable to CSV
- **Credential vault** — store passwords and SSH keys, encrypted at rest

### UX & Design
- **Operator Glass design system** — glassmorphic UI built on Material Design 3 color tokens
- **Light / dark theme** — auto-detects `prefers-color-scheme`; user-toggleable at runtime
- **Fully responsive** — works on desktop and tablets
- See [frontend/DESIGN_SYSTEM.md](frontend/DESIGN_SYSTEM.md) for the full token reference

---

## Tech Stack

**Frontend**
![Angular](https://img.shields.io/badge/Angular_21-DD0031?style=flat-square&logo=angular&logoColor=white)
![TypeScript](https://img.shields.io/badge/TypeScript-3178C6?style=flat-square&logo=typescript&logoColor=white)
![Tailwind CSS](https://img.shields.io/badge/Tailwind_CSS_4-06B6D4?style=flat-square&logo=tailwindcss&logoColor=white)
![nginx](https://img.shields.io/badge/nginx-009639?style=flat-square&logo=nginx&logoColor=white)

**Backend**
![.NET 10](https://img.shields.io/badge/.NET_10-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-512BD4?style=flat-square&logo=csharp&logoColor=white)
![EF Core](https://img.shields.io/badge/EF_Core_10-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![MediatR](https://img.shields.io/badge/MediatR-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![MailKit](https://img.shields.io/badge/MailKit-0078D4?style=flat-square&logo=maildotru&logoColor=white)
![Swagger](https://img.shields.io/badge/Swagger-85EA2D?style=flat-square&logo=swagger&logoColor=black)

**Data & Cache**
![PostgreSQL](https://img.shields.io/badge/PostgreSQL_18-4169E1?style=flat-square&logo=postgresql&logoColor=white)
![Redis](https://img.shields.io/badge/Redis_7-FF4438?style=flat-square&logo=redis&logoColor=white)

**Authentication**
![JWT](https://img.shields.io/badge/JWT_HS256-000000?style=flat-square&logo=jsonwebtokens&logoColor=white)
![OIDC](https://img.shields.io/badge/OIDC-F78C40?style=flat-square&logo=openid&logoColor=white)
![SAML 2.0](https://img.shields.io/badge/SAML_2.0-E8162A?style=flat-square&logoColor=white)

**Remote Access**
![Apache Guacamole](https://img.shields.io/badge/Apache_Guacamole_1.6-0B1A2C?style=flat-square&logo=apache&logoColor=white)
![guacamole-common-js](https://img.shields.io/badge/guacamole--common--js-0B1A2C?style=flat-square&logo=apache&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=flat-square&logo=docker&logoColor=white)

**Observability**
![Grafana](https://img.shields.io/badge/Grafana-F46800?style=flat-square&logo=grafana&logoColor=white)
![Prometheus](https://img.shields.io/badge/Prometheus-E6522C?style=flat-square&logo=prometheus&logoColor=white)
![Loki](https://img.shields.io/badge/Loki-F46800?style=flat-square&logo=grafana&logoColor=white)
![Tempo](https://img.shields.io/badge/Tempo-F46800?style=flat-square&logo=grafana&logoColor=white)
![OpenTelemetry](https://img.shields.io/badge/OpenTelemetry-425CC7?style=flat-square&logo=opentelemetry&logoColor=white)

**Testing & CI/CD**
![GitHub Actions](https://img.shields.io/badge/GitHub_Actions-2088FF?style=flat-square&logo=githubactions&logoColor=white)
![Vitest](https://img.shields.io/badge/Vitest-6E9F18?style=flat-square&logo=vitest&logoColor=white)
![Playwright](https://img.shields.io/badge/Playwright-2EAD33?style=flat-square&logo=playwright&logoColor=white)
![xUnit](https://img.shields.io/badge/xUnit-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![CodeQL](https://img.shields.io/badge/CodeQL-2088FF?style=flat-square&logo=github&logoColor=white)

**Docs**
![Docusaurus](https://img.shields.io/badge/Docusaurus_3-3ECC5F?style=flat-square&logo=docusaurus&logoColor=white)

---

## Quick Start

### Prerequisites
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose)
- Ports `4200`, `5000`, `3000`, `5432`, `6379`, `4822` available

### 1. Clone and start

```bash
git clone https://github.com/alessandrocaetanob/smooth-operator.git
cd smooth-operator
docker compose up --build
```

| Service | URL |
|---------|-----|
| **App** | http://localhost:4200 |
| **API** | http://localhost:5000 |
| **API Docs (Swagger)** | http://localhost:5000/swagger |
| **Docs site** | http://localhost:3000 |

### 2. First-access setup

Open http://localhost:4200. On first run the app redirects to the **setup wizard** where you create the initial Owner account, set a display name, and (optionally) configure SMTP.

### 3. Invite users

From **Settings → Users**, use the **Invite** button to send invite links to team members.

### 4. Create a vault and a connection

1. **Settings → Vaults** — create a vault (e.g. "Production Linux")
2. **Connections** — add an SSH/RDP/VNC connection, assign it to the vault
3. **Settings → Users** — assign users (or groups) to the vault

### 5. Connect

Users see their assigned connections under **My Access** or **My Vaults** and can launch a live session with one click.

---

## Running Services Independently

### Frontend (Angular)
```bash
cd frontend
npm install
npm start
# → http://localhost:4200
```

### Backend (.NET)
```bash
cd backend/src/SmoothOperator.Api
dotnet restore ../../../smooth-operator.sln
dotnet run
# → http://localhost:5000
# → Swagger UI at http://localhost:5000/swagger (Development only)
```

### Docs site (Docusaurus)
```bash
cd docs
npm install
npm start
# → http://localhost:3000
```

---

## Environment Variables

All variables follow the **Options pattern** — nested keys use `__` as separator in environment variables.  
See `.env.example` for a full template with generation hints.

### Required

| Variable | Service | Description |
|----------|---------|-------------|
| `ConnectionStrings__DefaultConnection` | backend | PostgreSQL connection string |
| `ConnectionStrings__Redis` | backend | Redis host:port |
| `Encryption__Key` | backend | 64-char hex (256-bit) — encrypts stored credentials. Generate: `openssl rand -hex 32` |
| `Jwt__Key` | backend | ≥32-byte random secret for HS256 signing. Generate: `openssl rand -hex 32` |

### Optional / Defaults

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` enables Swagger UI and detailed errors |
| `Cache__UseRedis` | `false` | Back the ASP.NET output cache with Redis instead of in-memory — recommended when running multiple backend replicas |
| `Jwt__Issuer` | `smooth-operator` | JWT issuer claim |
| `Jwt__Audience` | `smooth-operator-api` | JWT audience claim |
| `Jwt__AccessTokenExpirationMinutes` | `60` | Access token lifetime |
| `Jwt__RefreshTokenExpirationDays` | `7` | Refresh token lifetime |
| `Guacd__Host` | `guacd` | guacd service hostname |
| `Guacd__Port` | `4822` | guacd TCP port |
| `AppUrls__App` | `http://localhost:4200` | Public app URL (used in email links) |
| `AppUrls__Frontend` | `http://localhost:4200` | Frontend origin for CORS allow-list |
| `AppUrls__AllowedOrigins` | `[]` | Comma-separated extra CORS origins |
| `DataProtection__KeysPath` | `/data/protection-keys` | Where ASP.NET Data Protection writes key ring |
| `Otel__Endpoint` | *(disabled)* | OpenTelemetry OTLP gRPC endpoint |
| `Otel__ServiceName` | `smooth-operator-backend` | Service name in traces/metrics |

### SSO (optional)

| Variable | Description |
|----------|-------------|
| `AzureAd__Instance` | `https://login.microsoftonline.com/` — enables Entra ID SSO |
| `AzureAd__TenantId` | Entra ID tenant GUID |
| `AzureAd__ClientId` | Entra ID app client ID |
| `AzureAd__ClientSecret` | Entra ID client secret |

### SMTP (optional)

| Variable | Description |
|----------|-------------|
| `Smtp__Host` | SMTP server hostname |
| `Smtp__Port` | SMTP port (default `587`) |
| `Smtp__Username` | SMTP username |
| `Smtp__Password` | SMTP password |
| `Smtp__FromAddress` | From address for invite/reset emails |

### Frontend runtime config

| Variable | Default | Description |
|----------|---------|-------------|
| `APP_HELP_URL` | `http://localhost:3000` | Help link in sidebar |
| `APP_DOCS_URL` | `http://localhost:3000` | Docs link in login page |
| `APP_FEATURE_FLAGS` | `{}` | JSON feature-flag map |

> **Security:** The default `Encryption__Key` and `Jwt__Key` in `docker-compose.yml` are placeholders for **development only**. Always supply strong random secrets in non-local environments — via Docker secrets, a secrets manager, or a `.env` file that is **never committed**.

---

## Data Protection Key Encryption

ASP.NET Core's Data Protection system encrypts session cookies and antiforgery tokens. By default the key ring is stored as plaintext XML in the `dp_keys` Docker volume. For production deployments you should encrypt the key ring at rest using one of the options below.

### Option A — Azure Key Vault (Azure deployments)

1. Create or reuse an Azure Key Vault and add a **Key** (RSA 2048 or RSA-HSM 3072).
2. Grant your backend's managed identity (or service principal) the `Key Wrap` and `Key Unwrap` permissions on that key.
3. Pass the full key identifier URI to the backend:

```
DataProtection__AzureKeyVaultKeyId=https://my-vault.vault.azure.net/keys/dp-key
```

Authentication uses `DefaultAzureCredential` — it auto-detects managed identity in Azure, or falls back to `AZURE_CLIENT_ID` / `AZURE_CLIENT_SECRET` / `AZURE_TENANT_ID` environment variables for service-principal auth in other environments.

### Option B — Self-signed company certificate (self-hosted deployments)

**Step 1 — Generate the certificate (one-time):**

```bash
openssl req -x509 -newkey rsa:4096 -keyout dp.key -out dp.crt -days 3650 -nodes \
  -subj "/CN=smooth-operator-dp/O=Your Company"
openssl pkcs12 -export -out dp.pfx -inkey dp.key -in dp.crt -passout pass:
```

Store `dp.pfx` in a secure location (secrets manager or password vault). **Never commit it to the repository.**

**Step 2 — Mount it as a Docker secret in `docker-compose.override.yml`:**

```yaml
secrets:
  dp_cert:
    file: ./secrets/dp.pfx

services:
  backend:
    secrets:
      - dp_cert
    environment:
      - DataProtection__CertPath=/run/secrets/dp_cert
      # Omit DataProtection__CertPassword if the PFX has no password (passout pass: above)
```

**Key rotation:** when you issue a new certificate, keep the old one in the volume until all existing sessions have expired (ASP.NET Core's default key lifetime is 90 days). The runtime needs the old certificate to decrypt existing keys even after you switch to the new one for writing.

---

## CI/CD & Security

GitHub Actions workflows run on every push and PR:

| Workflow | What it checks |
|----------|---------------|
| **CodeQL** | Static analysis for C# and TypeScript vulnerabilities |
| **Dependency scan** | Known CVEs in npm and NuGet packages |
| **Docker scan** | Container image vulnerability scanning |
| **Build validation** | Frontend build + backend compile |
| **Format check** | Prettier (frontend) + dotnet format (backend) |
| **Tests** | Vitest unit tests (frontend) + xUnit integration tests (backend) |
| **Smoke Tests** | Playwright E2E browser automation |

---

## Testing

We maintain high code quality with automated test coverage enforcement (80% on Codecov `backend` flag / 70% frontend patch).

### Test Stack
- **Backend:** xUnit + FluentAssertions + WebApplicationFactory integration tests
- **Architecture:** NetArchTest.Rules — enforces Clean Architecture layer boundaries in CI
- **Frontend:** Vitest + Angular Testing Library
- **E2E/Smoke:** Playwright
- **Coverage:** Coverlet (Cobertura) + ReportGenerator

### Running Tests

#### Backend

```bash
# From repo root — runs all 151 tests (142 integration + 9 architecture)
dotnet test smooth-operator.sln
```

#### Backend with Coverage Report

```bash
dotnet test smooth-operator.sln \
  --settings coverlet.runsettings \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:coveragereport -reporttypes:Html
```

#### Frontend

```bash
cd frontend
npm test              # watch mode
npm test -- --run     # single-run (CI)
```

### Testing Decisions
1. **Impersonation headers** — integration tests inject `X-Test-UserId` / `X-Test-Roles` headers to simulate any user role without JWT ceremony.
2. **Real EF with SQLite** — integration tests use EF Core + in-memory SQLite for per-test isolation; no mocked repositories.
3. **Architecture tests** — `SmoothOperator.ArchitectureTests` asserts layer dependency rules using NetArchTest so violations fail CI immediately.
4. **Output cache invalidation** — write endpoints evict related cache tags (`EvictByTagAsync`) so subsequent GETs in tests receive fresh data.

Workflow details: [.github/workflows/README.md](.github/workflows/README.md)

---

## Performance

Performance work spans the frontend bundle, the API, and the container infrastructure.

### Frontend
- **esbuild application builder** with fully lazy-loaded feature routes and `OnPush` change detection on list-heavy pages.
- **Precompressed static assets** — JS/CSS/HTML are compressed to `.br` and `.gz` at build time and served via nginx `brotli_static` / `gzip_static` (no per-request compression). The `ngx_brotli` module is compiled in a dedicated Docker build stage.
- **Bundle analysis** — `npm run analyze` renders a local source-map treemap; CI uploads bundle stats to CodeCov for per-PR size tracking.

### Backend
- **EF Core `DbContextPool`** + Npgsql connection pooling; `AsNoTracking` reads and `SplitQuery`.
- **Response compression** (Brotli + Gzip) and an **output cache** (`ShortCache` policy) — in-memory by default, Redis-backed via `Cache__UseRedis`.
- **Trigram search** — `pg_trgm` expression GIN indexes make case-insensitive audit-log/user substring filters index-backed instead of sequential scans.
- **MediatR metrics** — every command/query records a Prometheus duration histogram (`smooth_operator_mediatr_request_duration_seconds`).

### Infrastructure
- **Tuned PostgreSQL** (`shared_buffers`, planner costs, `wal_compression`, `pg_stat_statements`) and **Redis** (`maxmemory` + `allkeys-lru`, persistence off) via `docker-compose.yml`.
- **Docker** — multi-stage builds with BuildKit cache mounts, Alpine runtime images, and per-service resource limits.
- **nginx** — HTTP/2, an upstream keepalive pool for the API proxy, and tuned worker settings.

### Measurement tooling
- **k6 load test** — `k6 run load-tests/smoke.js` exercises the auth + connections hot paths (see [load-tests/README.md](load-tests/README.md)).
- **BenchmarkDotNet** — micro-benchmarks under `backend/benchmarks/SmoothOperator.Benchmarks` (run with `dotnet run -c Release`).

---

## Documentation

Full user guides, admin reference, integration guides, and API documentation are available in the **Smooth Operator Docs** site.

```bash
# Start only the docs container
docker compose up docs
# → http://localhost:3000
```

| Section | Contents |
|---------|---------|
| [Getting Started](http://localhost:3000/docs/getting-started) | Installation, first setup, prerequisites |
| [User Guide](http://localhost:3000/docs/user-guide) | My Access, active sessions, profile |
| [Admin Guide](http://localhost:3000/docs/admin-guide) | Users, groups, vaults, connections, credentials, SSO, SMTP |
| [API Reference](http://localhost:3000/docs/api-reference) | Interactive Swagger UI + integration examples |
