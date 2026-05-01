# Smooth Operator 🕶️ 🔐

**Smooth Operator** is a cloud-native, clientless remote access vault. It gives teams secure, browser-based access to SSH, RDP, and VNC servers—without VPN clients, exposed ports, or direct credential sharing.

End users never see actual credentials. Admins control exactly who can access what, through granular role-based permissions and vault assignments.

> 📖 **Full documentation →** [http://localhost:3000](http://localhost:3000) *(start the docs container with `docker-compose up docs`)*

---

## 🏗️ Architecture

```mermaid
%%{init: {'theme': 'base', 'themeVariables': {'primaryColor': '#6750A4', 'primaryTextColor': '#FFFFFF', 'primaryBorderColor': '#4F378B', 'lineColor': '#6750A4', 'secondaryColor': '#EAD7FF', 'tertiaryColor': '#F6EDFF', 'background': '#FFFBFE', 'nodeBorder': '#4F378B', 'clusterBkg': '#F6EDFF', 'titleColor': '#21005D', 'edgeLabelBackground': '#F6EDFF'}}}%%
graph TD
    Browser([🌐 Browser]) -->|HTTPS + WebSocket| Frontend

    subgraph Docker Stack
        Frontend["⚡ Angular 21\nnginx · :4200"]
        Backend["🔧 .NET 10 API\n:5000"]
        DB[("🗄️ PostgreSQL 15\n:5432")]
        Cache[("⚡ Redis 7\n:6379")]
        Guacd["🖥️ guacd 1.6\nApache Guacamole\n:4822"]
        Docs["📚 Docusaurus\nDocs Site · :3000"]
    end

    Frontend -->|REST + WSS| Backend
    Backend -->|EF Core / Npgsql| DB
    Backend -->|StackExchange.Redis| Cache
    Backend -->|TCP 4822| Guacd
    Guacd -->|SSH / RDP / VNC| Targets[🖧 Target Servers]
```

### Component Breakdown

| Component | Technology | Role |
|-----------|-----------|------|
| **Frontend** | Angular 21, Tailwind CSS 4, guacamole-common-js | SPA served via nginx; renders live remote sessions on HTML5 Canvas |
| **Backend** | .NET 10, ASP.NET Core, EF Core | REST API + WebSocket tunnel; handles auth, RBAC, and Guacamole proxying |
| **Database** | PostgreSQL 15 | Users, vaults, connections, credentials, groups, audit logs |
| **Cache** | Redis 7 | Rate limiting, session state |
| **Connection Engine** | Apache guacd 1.6 | Translates RDP/SSH/VNC to Guacamole protocol over WebSocket |
| **Docs** | Docusaurus 3 | User guide, admin guide, and API reference |

---

## 🔌 Remote Session Flow

```mermaid
%%{init: {'theme': 'base', 'themeVariables': {'primaryColor': '#6750A4', 'primaryTextColor': '#FFFFFF', 'lineColor': '#6750A4', 'secondaryColor': '#EAD7FF', 'actorBkg': '#EAD7FF', 'actorBorder': '#6750A4', 'activationBkgColor': '#F6EDFF', 'activationBorderColor': '#6750A4', 'sequenceNumberColor': '#6750A4'}}}%%
sequenceDiagram
    actor User as 👤 User
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

## 🗄️ Data Model

```mermaid
%%{init: {'theme': 'base', 'themeVariables': {'primaryColor': '#6750A4', 'primaryTextColor': '#FFFFFF', 'lineColor': '#6750A4', 'secondaryColor': '#EAD7FF'}}}%%
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

## ✨ Features

### 🔐 Security & Access Control
- **Role-Based Access Control (RBAC)** — four built-in roles: `Owner`, `Admin`, `TeamAdmin`, `User`
- **Vault-based isolation** — users only see connections in vaults they're assigned to, never raw credentials
- **Invite-only registration** — no public self-registration; admins send email invites
- **Rate limiting** — fixed-window limiter on auth endpoints (5 req/min per IP)

### 🔑 Authentication
- **Local auth** — username/password with BCrypt hashing + HS256 JWT
- **SSO via OIDC** — plug in any OpenID Connect provider (Azure AD, Okta, Auth0, …)
- **SSO via SAML 2.0** — enterprise identity federation
- **Forgot-password flow** — email-based password reset (requires SMTP)

### 🖥️ Remote Sessions
- **RDP, SSH, VNC** — all three protocols via Apache Guacamole
- **Browser-native** — zero client software; runs on any modern browser
- **HTML5 Canvas rendering** — full keyboard/mouse capture via `guacamole-common-js`
- **Clipboard sharing** — bidirectional clipboard between browser and remote session
- **SSH key pair generation** — generate and store SSH keys directly in the vault

### 📋 Administration
- **User & group management** — create groups, assign members, bulk-grant vault access
- **Known hosts** — store and verify SSH host fingerprints
- **SMTP configuration** — connect any SMTP server for invite/reset emails; test with one click
- **Audit logs** — complete action history with IP addresses, exportable to CSV
- **Credential vault** — store passwords and SSH keys, encrypted at rest

### 🎨 UX & Design
- **Operator Glass design system** — glassmorphic UI built on Material Design 3 color tokens
- **Light / dark theme** — auto-detects `prefers-color-scheme`; user-toggleable at runtime
- **Fully responsive** — works on desktop and tablets
- See [frontend/DESIGN_SYSTEM.md](frontend/DESIGN_SYSTEM.md) for the full token reference

---

## 🛠️ Tech Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Frontend framework | Angular | 21 |
| Frontend styling | Tailwind CSS | 4 |
| Remote protocol rendering | guacamole-common-js | 1.6 |
| Backend framework | ASP.NET Core | .NET 10 |
| ORM | Entity Framework Core + Npgsql | 9 |
| Authentication | JWT HS256 + OIDC + SAML 2.0 | — |
| Database | PostgreSQL | 15 |
| Cache / rate limiter | Redis | 7 |
| Connection engine | Apache guacd | 1.6 |
| Email | MailKit | — |
| API docs | Swagger / OpenAPI | — |
| Docs site | Docusaurus | 3 |
| CI/CD | GitHub Actions | — |

---

## 🚀 Quick Start

### Prerequisites
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose)
- Ports `4200`, `5000`, `3000`, `5432`, `6379`, `4822` available

### 1. Clone and start

```bash
git clone https://github.com/alessandrocaetanob/smooth-operator.git
cd smooth-operator
docker-compose up --build
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

## 🔧 Running Services Independently

### Frontend (Angular)
```bash
cd frontend
npm install
npm start
# → http://localhost:4200
```

### Backend (.NET)
```bash
cd backend
dotnet restore
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

## ⚙️ Environment Variables

All variables are set in `docker-compose.yml`. Override them for production.

| Variable | Service | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | backend | `Development` enables Swagger UI and relaxed CORS |
| `ConnectionStrings__DefaultConnection` | backend | PostgreSQL connection string |
| `ConnectionStrings__Redis` | backend | Redis host:port |
| `Guacd__Host` | backend | guacd hostname (default `guacd`) |
| `Guacd__Port` | backend | guacd port (default `4822`) |
| `ENCRYPTION_KEY` | backend | 64-char hex key for encrypting stored credentials — **change in production!** |
| `Jwt__Key` | backend | HS256 JWT signing secret — **change in production!** |
| `Jwt__Issuer` | backend | JWT issuer claim (default `smooth-operator`) |
| `Jwt__Audience` | backend | JWT audience claim (default `smooth-operator-api`) |
| `APP_URL` | backend | Public app URL (used in email links) |
| `FRONTEND_URL` | backend | Frontend URL for CORS allow-list |
| `AzureAd__TenantId` | backend | *(optional)* Entra ID tenant — enables Microsoft SSO when set |
| `AzureAd__ClientId` | backend | *(optional)* Entra ID app client ID |
| `POSTGRES_USER/PASSWORD/DB` | postgres | PostgreSQL credentials |

> ⚠️ **Security:** The default `ENCRYPTION_KEY` and `Jwt__Key` in `docker-compose.yml` are for **development only**. Generate strong random secrets for any non-local deployment.

---

## 🔒 CI/CD & Security

GitHub Actions workflows run on every push and PR:

| Workflow | What it checks |
|----------|---------------|
| **CodeQL** | Static analysis for C# and TypeScript vulnerabilities |
| **Dependency scan** | Known CVEs in npm and NuGet packages |
| **Docker scan** | Container image vulnerability scanning |
| **Build validation** | Frontend build + backend compile |
| **Format check** | Prettier (frontend) + dotnet format (backend) |
| **Tests** | Vitest unit tests (frontend) + xUnit (backend) |

📖 Workflow details: [.github/workflows/README.md](.github/workflows/README.md) · [Quick Reference](.github/workflows/QUICKREF.md)

---

## 📖 Documentation

Full user guides, admin reference, integration guides, and API documentation are available in the **Smooth Operator Docs** site.

```bash
# Start only the docs container
docker-compose up docs
# → http://localhost:3000
```

| Section | Contents |
|---------|---------|
| [Getting Started](http://localhost:3000/docs/getting-started) | Installation, first setup, prerequisites |
| [User Guide](http://localhost:3000/docs/user-guide) | My Access, active sessions, profile |
| [Admin Guide](http://localhost:3000/docs/admin-guide) | Users, groups, vaults, connections, credentials, SSO, SMTP |
| [API Reference](http://localhost:3000/docs/api-reference) | Interactive Swagger UI + integration examples |
