---
sidebar_position: 10
---

# Audit Logs

The audit log is a complete, immutable record of all significant actions performed in Smooth Operator. It is visible to Admins and the Owner at **Settings → Audit Logs**.

## What is logged

Every audit log entry contains:

| Field | Description |
|-------|-------------|
| **Timestamp** | Date and time of the action (UTC) |
| **User** | Who performed the action (email) |
| **Action** | What was done (see action types below) |
| **Resource Type** | What kind of resource was affected (User, Connection, Vault, etc.) |
| **Resource ID** | The unique identifier of the affected resource |
| **Details** | Additional JSON context (e.g., changed fields) |
| **IP Address** | The client IP address of the request |

## Action types

| Action | Description |
|--------|-------------|
| `UserLogin` | Successful login |
| `UserLoginFailed` | Failed login attempt |
| `UserCreated` | New user registered |
| `UserUpdated` | User profile or role changed |
| `UserDeactivated` | User account deactivated |
| `ConnectionCreated` | New connection added |
| `ConnectionUpdated` | Connection parameters changed |
| `ConnectionDeleted` | Connection removed |
| `SessionStarted` | Remote session launched |
| `SessionEnded` | Remote session terminated |
| `VaultCreated` | New vault created |
| `VaultAssignmentChanged` | Vault access assignments updated |
| `CredentialCreated` | New credential stored |
| `CredentialDeleted` | Credential removed |
| `SettingsChanged` | System, SMTP, or SSO settings updated |

## Filtering

Use the filter bar to narrow by:
- **Date range** — from/to
- **User** — filter by email
- **Action** — select a specific action type
- **Resource Type** — filter by entity type

## Exporting

Click **Export** to download the current filtered view as a **CSV file**. This is useful for compliance, incident response, or feeding into a SIEM.

## Retention

Audit logs are stored indefinitely by default. A future settings option may allow configuring a retention window.

:::note
Audit log entries cannot be deleted through the UI. This is by design to maintain the integrity of the audit trail.
:::
