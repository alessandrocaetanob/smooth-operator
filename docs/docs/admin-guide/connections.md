---
sidebar_position: 5
---

# Connections

Connections are the core resource in Smooth Operator — each one represents a single remote server users can connect to.

## Supported protocols

| Protocol | Use case |
|----------|---------|
| **SSH** | Linux/Unix servers; terminal access; supports password and SSH key auth |
| **RDP** | Windows servers; full graphical desktop |
| **VNC** | Linux desktops, macOS screen sharing, any VNC server |

## Creating a connection

1. Go to **Connections**
2. Click **New Connection**
3. Fill in the required fields (see below)
4. Click **Save**

## Connection fields

### General

| Field | Required | Description |
|-------|:--------:|-------------|
| **Name** | ✅ | Friendly display name shown to users |
| **Protocol** | ✅ | SSH, RDP, or VNC |
| **Vault** | ✅ | Which vault this connection belongs to |

### Network

| Field | Required | Default | Description |
|-------|:--------:|---------|-------------|
| **Host** | ✅ | — | IP address or hostname of the target server |
| **Port** | ✅ | 22 / 3389 / 5900 | Port for the selected protocol |

### Authentication

| Field | Required | Description |
|-------|:--------:|-------------|
| **Credential** | ❌ | Select a stored [credential](./credentials) for automatic login |

If no credential is provided, the Guacamole session may prompt the user for a username/password depending on the protocol.

### SSH-specific options

| Option | Description |
|--------|-------------|
| **Known Host** | Select a stored SSH host fingerprint for verification |
| **Private Key** | SSH key-based auth — select a key from [Credentials](./credentials) |

## Connection probing

Smooth Operator can probe whether a connection's host:port is reachable. Click the **Probe** button on any connection row to run a real-time connectivity check.

The probe result is displayed as a status indicator:
- 🟢 **Reachable** — TCP connection to the host:port succeeds
- 🔴 **Unreachable** — connection refused or timeout
- ⚪ **Unknown** — probe not yet run

## Editing a connection

Click the edit icon on any connection row. Changes take effect immediately for new sessions — active sessions are not affected.

## Deleting a connection

Click the trash icon. Active sessions using this connection are not forcibly terminated but users can no longer launch new sessions.

## File transfer

Some connection types support file transfer. Click the paperclip icon on an active session to open the file transfer panel (available when the backend enables this feature).
