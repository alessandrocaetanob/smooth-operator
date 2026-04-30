---
sidebar_position: 4
---

# Vaults

Vaults are containers that group related connections together. Access control in Smooth Operator is vault-based: you grant users (or groups) access to a vault, and they can see all connections in it.

## Creating a vault

1. Go to **Settings → Vaults**
2. Click **New Vault**
3. Enter a name (e.g., "Production", "Dev Servers", "Customer A")
4. Click **Create**

## Adding connections to a vault

Connections are assigned to a vault when the connection is created or edited. See [Connections](./connections) for how to add SSH, RDP, or VNC connections.

## Assigning users and groups

1. Open the vault from **Settings → Vaults**
2. Click **Manage Assignments**
3. Add users and/or groups from the search dropdown
4. Click **Save**

Assignments take effect immediately. Removed assignments also take effect immediately.

## Viewing effective users

Click **Effective Users** on any vault to see the full list of users who can currently access it, including those who gain access through group membership.

## Renaming a vault

Click the edit icon in the vault row and update the name.

## Deleting a vault

Click the trash icon. This removes the vault and its assignments. **Connections inside the vault are not deleted** — they become unassigned and will no longer be visible to any user until reassigned to a new vault.

:::caution
Make sure to reassign orphaned connections after deleting a vault, or users will lose access to those sessions.
:::
