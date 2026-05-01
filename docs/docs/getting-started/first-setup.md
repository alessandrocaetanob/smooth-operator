---
sidebar_position: 4
---

# First Setup

The first time you open Smooth Operator, the app detects that no admin account exists and redirects you to the **setup wizard**. This only happens once.

## Step 1 — Create the Owner account

Fill in the setup form:

| Field | Description |
|-------|-------------|
| **Display Name** | Your name as it appears in the app |
| **Email** | Used to log in |
| **Password** | Must be at least 8 characters |

Click **Create Account**. This creates the root `Owner` account with full access to everything.

:::warning
There is only one Owner. This account cannot be demoted or deleted. Keep your password safe — if you lose access, you'll need to reset the database.
:::

## Step 2 — Log in

After setup, the app redirects you to the login page. Log in with the credentials you just created.

## Step 3 — (Optional) Configure SMTP

To send invite emails and password-reset links, you need a working SMTP configuration.

Go to **Settings → Email** and fill in your SMTP details:

| Field | Example |
|-------|---------|
| Host | `smtp.example.com` |
| Port | `587` |
| Username | `noreply@example.com` |
| Password | your SMTP password |
| From Address | `noreply@example.com` |
| From Name | `Smooth Operator` |
| Enable SSL | Yes (recommended) |

Click **Save**, then **Test** to verify the connection.

:::tip
If you don't have an SMTP server, you can use [Mailtrap](https://mailtrap.io/), [Mailhog](https://github.com/mailhog/MailHog), or your email provider's SMTP settings. Gmail SMTP requires an [App Password](https://support.google.com/accounts/answer/185833) if 2FA is enabled.
:::

If SMTP is not configured, invite links won't work. You can still create users and share invite links manually.

## Step 4 — Invite your first user

Go to **Settings → Users** and click **Invite User**.

Enter the email address and select a role:
- **Admin** — can manage everything
- **TeamAdmin** — can manage connections in assigned vaults
- **User** — connect-only access

If SMTP is configured, an invitation email is sent automatically. Otherwise, copy the invite link and share it manually.

## Step 5 — Create a vault

Go to **Settings → Vaults** and click **New Vault**.

Give it a descriptive name (e.g., "Production Linux", "Dev Servers", "Windows Farm").

## Step 6 — Add a connection

Go to **Connections** and click **New Connection**.

Fill in the details:
- **Name** — friendly display name
- **Protocol** — SSH, RDP, or VNC
- **Host** — IP address or hostname of the target server
- **Port** — default: 22 (SSH), 3389 (RDP), 5900 (VNC)
- **Vault** — select the vault to add this connection to
- **Credential** — optional; select a stored credential or leave blank to prompt the user

## Step 7 — Assign users to the vault

Go to **Settings → Vaults**, click the vault, then **Manage Assignments**.

Add the users or groups that should have access to this vault's connections.

## Step 8 — Connect!

Log in as a non-Owner user and go to **My Access** or **My Vaults**. Click a connection to launch a live session.

---

## What's next?

- [User Guide](../user-guide) — how to use the remote session features
- [Admin Guide](../admin-guide) — detailed admin reference for all settings
- [API Reference](../api-reference) — integrate Smooth Operator via REST API
