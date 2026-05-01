---
sidebar_position: 2
---

# Users

User management is found at **Settings → Users**.

## Roles

| Role | Invite users | Manage vaults | Manage connections | Manage credentials | Settings |
|------|:---:|:---:|:---:|:---:|:---:|
| **Owner** | Yes | Yes | Yes | Yes | Yes |
| **Admin** | Yes | Yes | Yes | Yes | Yes |
| **TeamAdmin** | No | No | Yes (assigned) | Yes | No |
| **User** | No | No | No | No | No |

## Inviting a user

1. Go to **Settings → Users**
2. Click **Invite User**
3. Enter the email address and select a role
4. Click **Send Invite**

If SMTP is configured, an email is sent automatically with a registration link valid for 48 hours. If SMTP is not configured, copy the invite link from the dialog and share it manually.

When the user clicks the link, they're prompted to set a display name and password. Their account becomes active immediately after registration.

## Managing existing users

The users table shows all registered users with their email, display name, role, and active status.

### Changing a role

Click the role badge next to a user's name and select the new role from the dropdown. Role changes take effect immediately on the user's next API request.

:::warning
You cannot demote another Owner. The Owner role can only be transferred by the current Owner (contact your database administrator if the Owner account is inaccessible).
:::

### Activating / deactivating accounts

Toggle the **Active** switch on any user row. Deactivated users are blocked from logging in and their existing JWT tokens are invalidated.

### Deleting a user

Click the trash icon. This permanently removes the user. Their actions remain in the [Audit Logs](./audit-logs).

## Vault assignments

You can view and edit a user's vault assignments from their profile row. Click the user's name, then **Vaults**. This shows both directly assigned vaults and vaults inherited via group membership.
