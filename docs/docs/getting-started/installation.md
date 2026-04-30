---
sidebar_position: 3
---

# Installation

The fastest way to run Smooth Operator is with Docker Compose. The entire stack (frontend, backend, database, cache, connection engine, and docs) spins up with a single command.

## Step 1 — Clone the repository

```bash
git clone https://github.com/alessandrocaetanob/smooth-operator.git
cd smooth-operator
```

## Step 2 — (Optional) Review environment variables

The default `docker-compose.yml` works out of the box for local development. For any deployment beyond your local machine, open `docker-compose.yml` and change these values:

```yaml
environment:
  # ⚠️ Generate a strong random 64-char hex secret
  - ENCRYPTION_KEY=<your-64-char-hex>
  # ⚠️ Generate a strong random secret
  - Jwt__Key=<your-jwt-secret>
  # Set to your public app URL
  - APP_URL=https://your-domain.com
  - FRONTEND_URL=https://your-domain.com
```

You can generate secure random keys with:

```bash
# Linux / macOS
openssl rand -hex 32      # 64-char hex for ENCRYPTION_KEY
openssl rand -base64 48   # strong random for Jwt__Key

# Windows PowerShell
[System.BitConverter]::ToString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) -replace '-',''
```

## Step 3 — Start the stack

```bash
docker-compose up --build
```

First-time build downloads Docker images and compiles the application — expect **3–5 minutes** on a fast internet connection.

## Step 4 — Verify the services

Once running, all services should be accessible:

| Service | URL | Status |
|---------|-----|--------|
| **App** | http://localhost:4200 | Angular SPA |
| **API** | http://localhost:5000 | .NET REST API |
| **Swagger UI** | http://localhost:5000/swagger | API documentation |
| **Docs** | http://localhost:3000 | This site |

Run `docker-compose ps` to confirm all containers are healthy.

## Step 5 — Complete first-time setup

Open http://localhost:4200. The app will redirect you to the **setup wizard** if no admin account exists yet. See [First Setup](./first-setup) for a walkthrough.

---

## Starting individual services

You can start only specific services if needed:

```bash
# Start everything except docs
docker-compose up frontend backend postgres redis guacd

# Start only the docs site
docker-compose up docs

# Start only the database and cache (for backend dev mode)
docker-compose up postgres redis
```

## Stopping the stack

```bash
# Stop all containers (preserves data volumes)
docker-compose down

# Stop and remove volumes (⚠️ deletes all data)
docker-compose down -v
```

## Updating

```bash
git pull
docker-compose up --build
```

EF Core migrations run automatically on backend startup — your database schema is always up to date.

---

## Running in development mode (without Docker)

See [System Requirements](./requirements) for required tool versions.

### Frontend
```bash
cd frontend
npm install
npm start
# → http://localhost:4200 (live reload)
```

### Backend
```bash
cd backend
dotnet restore
dotnet run
# → http://localhost:5000
# → Swagger UI at http://localhost:5000/swagger
```

### Docs
```bash
cd docs
npm install
npm start
# → http://localhost:3000 (live reload)
```

You'll need a running PostgreSQL instance and Redis. The easiest way is to start just those containers:
```bash
docker-compose up postgres redis guacd -d
```
