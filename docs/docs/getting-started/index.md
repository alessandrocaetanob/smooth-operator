---
sidebar_position: 1
---

# Introduction

**Smooth Operator** is a cloud-native, clientless remote access vault. It gives teams secure, browser-based access to SSH, RDP, and VNC servers—without VPN clients, exposed ports, or direct credential sharing.

## Why Smooth Operator?

Traditional remote access tools require one of the following:
- Installing a VPN client on every user's machine
- Distributing SSH keys or RDP passwords directly to users
- Exposing server ports to the public internet

Smooth Operator eliminates all three risks. Users log in through a browser and see only the connections they're authorized to use. They never interact with raw credentials.

## How It Works

```
Browser → Angular SPA → .NET API → guacd → Target Server
                           ↓
                      PostgreSQL (users, vaults, audit)
                      Redis (sessions, rate limiting)
```

1. **Admin** creates a _connection_ (host, port, protocol, credentials) and places it in a _vault_
2. **Admin** assigns users or groups to that vault
3. **User** logs in, sees their assigned connections, clicks to connect
4. **API** verifies permission, issues a one-time ticket, proxies the session through `guacd`
5. **User** sees a live remote desktop in their browser — no client software needed

## Roles

| Role | What they can do |
|------|-----------------|
| **Owner** | Everything — created during the first-setup wizard |
| **Admin** | Manage users, groups, vaults, credentials, connections, settings |
| **TeamAdmin** | Manage connections inside vaults they're assigned to |
| **User** | Launch sessions in their assigned vaults |

## Next Steps

- [System Requirements](./requirements) — check hardware and network prerequisites
- [Installation](./installation) — deploy with Docker Compose in under 5 minutes
- [First Setup](./first-setup) — create your admin account and configure email
