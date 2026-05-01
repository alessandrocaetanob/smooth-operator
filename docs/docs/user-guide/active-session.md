---
sidebar_position: 3
---

# Active Sessions

Once a connection is established, you'll see a full-screen remote desktop rendered in your browser via **HTML5 Canvas**.

## The session view

| Area | Description |
|------|-------------|
| **Canvas** | The live remote desktop — all mouse and keyboard input is captured |
| **Header bar** | Shows the connection name; contains the disconnect button |

## Keyboard and mouse

- Mouse clicks, movement, and scroll are forwarded to the remote session automatically
- All keyboard input is forwarded — including Ctrl, Alt, function keys, etc.
- **Ctrl+Alt+Del** can be sent via the session toolbar if your browser intercepts it

## Clipboard sharing

Smooth Operator supports bidirectional clipboard sharing between your browser and the remote session:

- **Copy from remote** — highlight text in the remote session; it becomes available in your local clipboard
- **Paste to remote** — copy text locally, then click inside the canvas and paste (Ctrl+V)

:::note
Clipboard sharing requires that your browser grants clipboard permissions. You may see a permission prompt the first time.
:::

## Disconnecting

Click the **X / Disconnect** button in the session header, or simply close the browser tab. The session is cleanly terminated — no dangling processes are left on the server.

## Session recording

Smooth Operator logs session start and end events in the [Audit Logs](../admin-guide/audit-logs). Full session recording (video) is not currently supported.

## Reconnecting

After a disconnect you can relaunch the same connection from [My Access](./my-access). There is no persistent session state — each connection opens a fresh remote session.

## Troubleshooting

| Symptom | Likely cause | Resolution |
|---------|-------------|-----------|
| Black screen after connecting | Remote desktop service not running | Check the target server's SSH/RDP/VNC daemon |
| Connection refused immediately | Wrong host or port | Contact your administrator |
| Very slow / laggy session | Network latency or high server load | Try a different network; check server CPU usage |
| Keyboard not working | Browser focus lost | Click once in the canvas area |
