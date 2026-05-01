---
sidebar_position: 1
---

# API Overview

Smooth Operator exposes a **REST API** over HTTP/HTTPS. All endpoints return JSON. The API is used by the Angular frontend and is fully available to third-party integrators.

## Base URL

| Environment | Base URL |
|-------------|---------|
| Docker Compose (local) | `http://localhost:5000/api` |
| Production | `https://your-domain.com/api` |

## Authentication

The API uses **Bearer token authentication**. Tokens are JWT (HS256), issued by the `/api/auth/login` endpoint.

### Obtaining a token

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "your-password"
}
```

Response:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-01-01T00:00:00Z",
  "user": {
    "id": "...",
    "email": "user@example.com",
    "displayName": "Alice",
    "role": "Admin"
  }
}
```

### Using the token

Include the token in the `Authorization` header of every authenticated request:

```http
GET /api/users
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

## API endpoints overview

| Resource | Base Path | Description |
|---------|-----------|-------------|
| Auth | `/api/auth` | Login, register, invite, password reset, SSO |
| Users | `/api/users` | User CRUD, role management |
| Groups | `/api/groups` | Group CRUD, member management |
| Vaults | `/api/vaults` | Vault CRUD, assignments |
| Connections | `/api/connections` | Connection CRUD, probe |
| Credentials | `/api/credentials` | Credential CRUD, SSH key generation |
| Known Hosts | `/api/hosts` | SSH host fingerprint management |
| Guacamole | `/api/guacamole` | Issue session tickets, open WebSocket sessions |
| SSO | `/api/auth/sso` | OIDC / SAML flows |
| Settings — SMTP | `/api/settings/smtp` | Email configuration |
| Settings — SSO | `/api/settings/sso` | SSO provider configuration |
| Audit Logs | `/api/audit-logs` | Query and export logs |
| Invites | `/api/invites` | Look up and redeem invite tokens |

## Error responses

All errors follow this shape:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "errors": {
    "email": ["The email field is required."]
  }
}
```

| HTTP Status | Meaning |
|-------------|---------|
| `400` | Validation error — check the `errors` object |
| `401` | Unauthorized — missing or invalid Bearer token |
| `403` | Forbidden — you don't have the required role |
| `404` | Resource not found |
| `409` | Conflict — resource already exists |
| `429` | Too many requests — rate limit exceeded |
| `500` | Internal server error |

## Rate limiting

Auth endpoints are rate-limited to **5 requests per minute** per IP address. Exceeding the limit returns HTTP `429 Too Many Requests`.

## Enabling Swagger UI

The interactive Swagger UI is available in **Development** mode:

```
http://localhost:5000/swagger
```

Set `ASPNETCORE_ENVIRONMENT=Development` in your environment to enable it. See the [Interactive Swagger UI](./swagger) page for an embedded version.

## Pagination

Endpoints that return lists (e.g., `/api/users`, `/api/audit-logs`) support optional `page` and `pageSize` query parameters:

```
GET /api/audit-logs?page=1&pageSize=25
```

The response includes pagination metadata:
```json
{
  "items": [...],
  "total": 142,
  "page": 1,
  "pageSize": 25
}
```
