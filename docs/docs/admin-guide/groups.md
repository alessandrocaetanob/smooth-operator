---
sidebar_position: 3
---

# Groups

Groups let you assign vault access to multiple users at once. Instead of adding each user to a vault individually, you create a group, add users to it, and assign the group to vaults.

## Creating a group

1. Go to **Settings → Groups**
2. Click **New Group**
3. Enter a name (e.g., "Linux Ops", "Windows Team", "Dev Access")
4. Click **Create**

## Managing members

Click any group to open it, then click **Members**.

- **Add members** — select users from the dropdown and confirm
- **Remove members** — click the remove icon next to a user

Group membership changes take effect immediately. A user who is added to a group immediately gains access to all vaults the group is assigned to.

## Assigning groups to vaults

After creating a group, assign it to one or more vaults from **Settings → Vaults** (see [Vaults → Assignments](./vaults#assigning-users-and-groups)).

Alternatively, from the group detail view, click **Vaults** to see the vaults this group is assigned to.

## Effective access

A user gains access to a vault if:
- They are **directly assigned** to the vault, **or**
- They belong to a **group** that is assigned to the vault

Both paths are combined. Removing a user from a group or removing the group's vault assignment takes effect immediately.

## Deleting a group

Click the trash icon in the groups table. Deleting a group removes it from all vault assignments. Users who were only in that group lose access to those vaults immediately.
