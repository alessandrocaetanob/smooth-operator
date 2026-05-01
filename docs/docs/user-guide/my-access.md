---
sidebar_position: 2
---

# My Access

**My Access** is your personal dashboard showing every connection you're authorized to launch. Connections are organized by the vaults they belong to.

## Accessing My Access

Click **My Access** in the left sidebar (the shield icon).

## Finding a connection

Connections are listed under their vault names. Use the search bar at the top to filter by name, host, or protocol.

Each connection card shows:
- Connection name
- Protocol badge (SSH / RDP / VNC)
- Hostname or IP address
- Last connected timestamp (if applicable)

## Launching a session

Click the **Connect** button on any connection card. The app will:

1. Request a one-time ticket from the API
2. Verify your permissions
3. Open a loading screen while the connection is established
4. Launch the live remote session in the browser

See [Active Sessions](./active-session) for what to do once connected.

## My Vaults

**My Vaults** shows the same connections in a larger card layout organized by vault. Click any vault to expand it and see the connections inside.

## Connection probing

Some connections show a status indicator (green / red / gray) showing whether the remote host is reachable. This is based on the last successful probe.

:::tip
If a connection shows red, the server may be unreachable, powered off, or the port might be wrong. Contact your administrator.
:::
