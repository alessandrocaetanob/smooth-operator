---
sidebar_position: 1
---

# Admin Guide Overview

This guide covers all administrative tasks in Smooth Operator. It is intended for users with the **Admin** or **Owner** role.

## What admins can do

| Area | Description |
|------|-------------|
| [Users](./users) | Invite users, manage roles, activate/deactivate accounts |
| [Groups](./groups) | Create groups, manage members, assign groups to vaults |
| [Vaults](./vaults) | Create and manage vaults; control access assignments |
| [Connections](./connections) | Add SSH/RDP/VNC connections; configure parameters and credentials |
| [Credentials](./credentials) | Store passwords and SSH keys securely |
| [SMTP Settings](./settings-smtp) | Configure email for invites and password resets |
| [SSO Settings](./settings-sso) | Configure OIDC or SAML identity providers |
| [Audit Logs](./audit-logs) | View and export the full action history |

## Settings area

All administrative settings are under the **Settings** menu in the left sidebar. The Settings section is only visible to Admins and the Owner.

## Role hierarchy

```
Owner
  └── Admin
        └── TeamAdmin
              └── User
```

Higher roles inherit all capabilities of the roles below them. Only the Owner can assign the Admin role.

## Common workflows

- **Onboard a new team member** → [Invite a user](./users#inviting-a-user), [assign to a vault](./vaults#assigning-users-and-groups)
- **Add a new server** → [Create a credential](./credentials), [create a connection](./connections), [assign to a vault](./vaults#assigning-users-and-groups)
- **Set up Single Sign-On** → [SSO Settings](./settings-sso)
- **Review recent activity** → [Audit Logs](./audit-logs)
