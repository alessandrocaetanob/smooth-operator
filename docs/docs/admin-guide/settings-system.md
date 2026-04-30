---
sidebar_position: 9
---

# System Settings

System settings control application-level behavior. Go to **Settings → System**.

## Available settings

| Setting | Description |
|---------|-------------|
| **Application Name** | The display name shown in the browser tab and email headers |
| **Allow Self Registration** | When enabled, anyone with the app URL can register without an invite. **Disabled by default.** |
| **Session Timeout** | Inactivity period (in minutes) before a user's JWT expires and they must log in again |

## Allow Self Registration

By default, registration requires an invite link from an admin. Enabling self-registration opens the `/register` endpoint to anyone.

:::danger
Only enable self-registration if Smooth Operator is deployed in a trusted, private network. On a public-facing deployment, this allows anyone to create an account.
:::

This can also be overridden via environment variable:
```yaml
- Auth__AllowSelfRegister=true
```

## Saving changes

Click **Save** after updating any setting. Changes take effect immediately without restarting the application.
