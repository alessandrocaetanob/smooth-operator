---
sidebar_position: 9
---

# Azure Key Vault Integration

Smooth Operator can store and retrieve credential secrets from **Azure Key Vault**. This allows security and compliance teams to manage secrets centrally — including rotation — without touching the application database.

## Overview

| Flow | Description |
|------|-------------|
| **Push** | Enter the secret in Smooth Operator; it is written to Key Vault and the reference is saved. The plaintext never persists in the database. |
| **Link** | Point to an existing secret already in Key Vault. Smooth Operator stores only the secret name. |
| **Fetch** | At connection time, the secret is retrieved fresh from Key Vault on every session. |

---

## Prerequisites

### 1 — Create an Azure Key Vault

In the [Azure Portal](https://portal.azure.com), create a Key Vault in your subscription. Note the **Vault URI** (e.g. `https://my-company-kv.vault.azure.net/`).

### 2 — Create an Entra ID App Registration

1. Go to **Microsoft Entra ID → App registrations → New registration**.
2. Give it a name (e.g. `smooth-operator-kv`).
3. Leave the redirect URI blank — this is a service-to-service integration.
4. After creation, note the **Application (client) ID** and **Directory (tenant) ID** from the Overview blade.

### 3 — Create a Client Secret

1. Under the app registration, go to **Certificates & secrets → New client secret**.
2. Choose a sensible expiry and click **Add**.
3. **Copy the secret value immediately** — you cannot view it again.

### 4 — Assign Key Vault Roles

Grant the app registration access to your Key Vault using Azure RBAC:

| Operation | Required role |
|-----------|--------------|
| Push new secrets to Key Vault | `Key Vault Secrets Officer` |
| Read secrets at connect time | `Key Vault Secrets User` |

To assign:
1. Open your Key Vault → **Access control (IAM) → Add role assignment**.
2. Select the role.
3. Under **Members**, choose **User, group, or service principal** and search for your app registration name.
4. Click **Review + assign**.

:::tip
If you only need read access (all secrets are pre-created in Key Vault), assign only `Key Vault Secrets User`. Use `Key Vault Secrets Officer` only if you want Smooth Operator to create secrets in Key Vault.
:::

---

## Configuring a Secret Provider in Smooth Operator

1. Log in as an **Owner** or **Admin**.
2. Go to **Settings → Secret Providers**.
3. Click **Add provider**.
4. Fill in the form:

| Field | Value |
|-------|-------|
| **Name** | A display name for this provider (e.g. `Production KV`) |
| **Vault URI** | Your Key Vault URI (e.g. `https://my-company-kv.vault.azure.net/`) |
| **Tenant ID** | Directory (tenant) ID from your app registration |
| **Client ID** | Application (client) ID from your app registration |
| **Client Secret** | The client secret value you copied |

5. Click **Test connection** to verify the credentials are correct before saving.
6. Click **Save**.

:::info
The client secret is AES-256 encrypted at rest in the database using the same key as other sensitive configuration. It is never returned to the browser after being saved.
:::

---

## Using Key Vault Secrets with Credentials

### Push a new secret to Key Vault

1. Go to **Credentials → New credential** (or edit an existing one).
2. Under **Storage**, switch from **Local** to **Azure Key Vault**.
3. Select a configured provider.
4. Enter the credential details including the secret value.
5. Click **Save** — the secret is written to Key Vault under the name `smooth-operator/{credential-name}-{id}` and only the reference is stored locally.

### Link an existing Key Vault secret

1. Create or edit a credential.
2. Under **Storage**, switch to **Azure Key Vault** and select the provider.
3. Select an existing secret from the **Secret name** dropdown (populated from your Key Vault).
4. Optionally pin a specific **version** (leave blank to always use the latest).
5. Click **Save**.

:::tip Rotation
When a secret is rotated in Key Vault, Smooth Operator automatically uses the latest version on the next connection — no changes needed in the app, unless you have pinned a specific version.
:::

---

## Troubleshooting

### Error codes

| Code | Meaning | Resolution |
|------|---------|-----------|
| `vault_auth_failed` | Authentication to Key Vault failed | Verify the Tenant ID, Client ID, and Client Secret. Check the client secret has not expired. |
| `vault_access_denied` | The app registration lacks permission | Assign `Key Vault Secrets User` (and `Key Vault Secrets Officer` if pushing) to the app registration. |
| `secret_not_found` | The referenced secret does not exist in Key Vault | Check the secret name in Key Vault. If it was deleted, restore it or re-link to a new secret. |
| `vault_unreachable` | Network or DNS error reaching Key Vault | Verify network connectivity from the Smooth Operator host to `https://<vault>.vault.azure.net`. Check firewall / NSG rules. |

### Test connection fails

1. Confirm the Vault URI is correct and ends with `/` or `.vault.azure.net`.
2. Ensure the app registration has at minimum `Key Vault Secrets User` on the Key Vault.
3. Make sure the client secret has not expired — generate a new one and update the provider.

### Connection fails at session time

If a session that previously worked suddenly fails with a vault error, the most common causes are:

- The **client secret** expired — rotate it and update the provider.
- The **referenced secret was deleted** — restore or re-link.
- A **network policy change** blocked outbound traffic to Key Vault.

Check **Audit Logs** in Smooth Operator and search for `secret.fetched` events with `outcome: failure` for details.
