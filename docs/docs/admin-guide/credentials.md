---
sidebar_position: 6
---

# Credentials

Credentials store authentication secrets that connections use to log in to remote servers automatically. Stored credentials are **encrypted at rest** — the plaintext secret is never returned by the API.

## Credential types

| Type | Use case |
|------|---------|
| **Password** | Username + password for SSH, RDP, or VNC |
| **SSH Key** | SSH private key (with optional passphrase) for SSH connections |

## Viewing credentials

Go to **Credentials** in the left sidebar (visible to Admins and TeamAdmins). The list shows credential names and types. **Secrets are never displayed.**

## Creating a credential

1. Go to **Credentials**
2. Click **New Credential**
3. Select the type (**Password** or **SSH Key**)
4. Fill in:
   - **Name** — a descriptive label (e.g., "prod-linux-deploy", "windows-admin")
   - **Username** — the remote login username
   - **Password / Private Key** — the secret value
5. Click **Save**

The secret is immediately encrypted with the server's `ENCRYPTION_KEY` and stored. You cannot retrieve it again — only replace it.

## Generating an SSH key pair

Smooth Operator can generate an SSH key pair for you:

1. Click **Generate SSH Key**
2. A fresh ED25519 key pair is generated server-side
3. The **public key** is shown — copy it and add it to your server's `~/.ssh/authorized_keys`
4. The **private key** is stored as a new credential

:::warning
The private key is shown **once** during generation. After saving, it cannot be retrieved. Store the public key on your target servers before closing the dialog.
:::

## Assigning a credential to a connection

When creating or editing a [Connection](./connections), select the credential from the **Credential** dropdown.

## Editing a credential

Click the edit icon. You can update the name and username. To update the secret, enter a new value in the secret field — leaving it blank preserves the existing secret.

## Deleting a credential

Click the trash icon. Connections that referenced this credential will lose their authentication method — update those connections to use a different credential.
