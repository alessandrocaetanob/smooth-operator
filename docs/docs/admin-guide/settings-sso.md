---
sidebar_position: 8
---

# SSO Settings

Smooth Operator supports Single Sign-On via **OIDC (OpenID Connect)** and **SAML 2.0**. SSO is optional — local username/password auth always works.

Go to **Settings → SSO** to configure.

## How SSO works

When SSO is configured and enabled:
1. The login page shows an additional **Sign in with [Provider]** button
2. Clicking it redirects the user to the identity provider
3. After authentication, the user is redirected back with a token
4. Smooth Operator creates or updates the user account and issues a JWT

Users who sign in via SSO for the first time get the **User** role by default. An admin can change their role afterward.

---

## OIDC Configuration

OIDC works with any OAuth 2.0 / OpenID Connect provider: Azure AD, Okta, Auth0, Google Workspace, Keycloak, etc.

### Step 1 — Create an app registration

In your identity provider:
1. Create a new application / client
2. Set the **Redirect URI** to: `http://your-app-url/auth/sso/finalize`
3. Note the **Client ID**, **Client Secret**, and **Authority URL** (discovery endpoint)

### Step 2 — Configure in Smooth Operator

Go to **Settings → SSO**, select **OIDC**, and fill in:

| Field | Description |
|-------|-------------|
| **Authority** | The OIDC discovery base URL (e.g., `https://login.microsoftonline.com/{tenant}/v2.0`) |
| **Client ID** | The application's client ID from your IdP |
| **Client Secret** | The application's client secret |

Click **Save**, then **Toggle** to enable.

### Azure AD / Entra ID — Environment variable method

For Azure AD, you can also configure SSO via environment variables in `docker-compose.yml`:

```yaml
environment:
  - AzureAd__TenantId=<your-tenant-id>
  - AzureAd__ClientId=<your-client-id>
```

This registers the Azure AD authentication scheme in addition to the standard OIDC flow.

---

## SAML 2.0 Configuration

SAML works with enterprise providers: Azure AD, ADFS, Okta, OneLogin, etc.

### Step 1 — Get the service provider metadata

Smooth Operator exposes its SAML metadata at:
```
GET /api/auth/sso/metadata
```

Download or copy this metadata XML and register it in your identity provider as a new SAML application.

### Step 2 — Configure in Smooth Operator

Go to **Settings → SSO**, select **SAML**, and fill in:

| Field | Description |
|-------|-------------|
| **IdP Metadata URL** | URL to your IdP's SAML metadata XML |
| **IdP Entity ID** | The IdP's entity identifier |
| **IdP SSO URL** | The IdP's single sign-on URL |

Click **Save**, then **Toggle** to enable.

---

## Testing SSO

1. Open the app in an **incognito / private window**
2. The login page should show a "Sign in with [Provider]" button
3. Click it and complete authentication with your IdP
4. You should be redirected back and logged in

Use **Settings → SSO → Test Connection** to verify the IdP metadata is reachable without logging out.

## Disabling SSO

Click **Delete** in the SSO settings to remove the provider configuration. The SSO button disappears from the login page immediately. Existing users who registered via SSO can still log in with a local password if they set one.
