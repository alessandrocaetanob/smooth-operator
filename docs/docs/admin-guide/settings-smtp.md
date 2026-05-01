---
sidebar_position: 7
---

# SMTP Settings

SMTP settings enable Smooth Operator to send transactional emails:
- **Invite emails** — when you invite a new user
- **Password reset emails** — when a user clicks "Forgot password?"

Go to **Settings → Email** to configure SMTP.

## Configuration fields

| Field | Description |
|-------|-------------|
| **Host** | SMTP server hostname (e.g., `smtp.gmail.com`, `smtp.sendgrid.net`) |
| **Port** | Usually `587` (STARTTLS) or `465` (SSL) |
| **Username** | SMTP account username or API key |
| **Password** | SMTP account password |
| **From Address** | The `From:` email address (e.g., `noreply@yourcompany.com`) |
| **From Name** | The display name in the `From:` header (e.g., `Smooth Operator`) |
| **Enable SSL** | Use TLS — recommended for ports 465 and 587 |

## Saving and testing

1. Fill in all fields and click **Save**
2. Click **Test** to send a test email to your own address
3. Verify the email arrives and check the spam folder if it doesn't

If the test fails, check:
- The hostname and port are correct
- Your SMTP credentials are valid
- Your provider doesn't require an App Password (see below)

## Provider-specific notes

### Gmail

Gmail requires an [App Password](https://support.google.com/accounts/answer/185833) if your account has 2-Step Verification enabled. Use:
- Host: `smtp.gmail.com`
- Port: `587`
- Username: your full Gmail address
- Password: the generated App Password

### SendGrid

- Host: `smtp.sendgrid.net`
- Port: `587`
- Username: `apikey` (literal string)
- Password: your SendGrid API key

### Microsoft 365 / Office 365

- Host: `smtp.office365.com`
- Port: `587`
- Username: your full email address
- Password: your M365 password or app password

## Without SMTP

If SMTP is not configured:
- The **Invite User** flow works but invite links must be copied and shared manually
- The **Forgot Password** flow is unavailable
- All other features function normally
