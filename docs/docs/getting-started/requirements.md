---
sidebar_position: 2
---

# System Requirements

## Hardware

| Resource | Minimum | Recommended |
|----------|---------|-------------|
| CPU | 2 cores | 4 cores |
| RAM | 2 GB | 4 GB |
| Disk | 5 GB | 20 GB |

The RAM requirement scales with the number of concurrent remote sessions. Each active Guacamole session uses roughly 50–150 MB depending on the protocol (VNC typically higher than SSH).

## Software

| Requirement | Version |
|-------------|---------|
| Docker Engine | 24+ |
| Docker Compose | v2.20+ |

No other software is required on the host machine. The Angular frontend, .NET backend, PostgreSQL database, Redis cache, and Guacamole engine all run as Docker containers.

For local development without Docker, you also need:

| Tool | Version |
|------|---------|
| Node.js | 22+ |
| .NET SDK | 10.0 |

## Network & Ports

The following ports must be available on the host:

| Port | Service | Description |
|------|---------|-------------|
| `4200` | Frontend | Angular SPA (nginx) |
| `5000` | Backend | .NET REST API + WebSocket |
| `3000` | Docs | Docusaurus documentation site |
| `5432` | PostgreSQL | Database (can be restricted to Docker network only) |
| `6379` | Redis | Cache (can be restricted to Docker network only) |
| `4822` | guacd | Guacamole engine (can be restricted to Docker network only) |

:::tip
For a production deployment you only need ports `4200` (or 443 if behind a reverse proxy) and `5000` exposed to users. The database, Redis, and guacd ports should **not** be publicly accessible.
:::

## Browser Support

| Browser | Minimum version |
|---------|----------------|
| Chrome / Chromium | 100+ |
| Firefox | 100+ |
| Edge | 100+ |
| Safari | 16+ |

Remote sessions render on an HTML5 Canvas element and use WebSockets. Any modern browser with WebSocket support will work.

## Target Servers

Smooth Operator connects to target servers via the following protocols:

| Protocol | Default Port | Notes |
|----------|-------------|-------|
| SSH | 22 | Password or SSH key authentication |
| RDP | 3389 | Windows Remote Desktop |
| VNC | 5900 | Virtual Network Computing |

The target server just needs to be reachable from the host running `guacd`. There is no agent to install on target servers.
