---
sidebar_position: 3
---

# Integration Guide

This guide shows how to integrate with Smooth Operator programmatically using the REST API.

## Step 1 — Obtain a token

All API calls (except auth endpoints) require a Bearer token.

### cURL

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email": "admin@example.com", "password": "your-password"}'
```

### JavaScript (fetch)

```js
const response = await fetch('http://localhost:5000/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email: 'admin@example.com', password: 'your-password' }),
});
const { token } = await response.json();
```

### Python

```python
import requests

resp = requests.post('http://localhost:5000/api/auth/login', json={
    'email': 'admin@example.com',
    'password': 'your-password',
})
token = resp.json()['token']
```

---

## Step 2 — Make authenticated requests

Include the token in the `Authorization` header:

### cURL

```bash
TOKEN="eyJhbGci..."

# List all connections
curl http://localhost:5000/api/connections \
  -H "Authorization: Bearer $TOKEN"

# List all users
curl http://localhost:5000/api/users \
  -H "Authorization: Bearer $TOKEN"
```

### JavaScript helper

```js
const API = 'http://localhost:5000/api';

async function apiGet(path, token) {
  const res = await fetch(`${API}${path}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`API error: ${res.status}`);
  return res.json();
}

// Usage
const connections = await apiGet('/connections', token);
const users = await apiGet('/users', token);
```

---

## Common workflows

### Create a connection programmatically

```bash
curl -X POST http://localhost:5000/api/connections \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "prod-web-01",
    "protocol": "SSH",
    "host": "10.0.0.50",
    "port": 22,
    "vaultId": "<vault-uuid>"
  }'
```

### Invite a user

```bash
curl -X POST http://localhost:5000/api/auth/invite \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "newuser@example.com",
    "role": "User"
  }'
```

Response includes an `inviteUrl` if SMTP is not configured, allowing you to share the link via your own notification system.

### Assign a user to a vault

```bash
# Get current vault assignments
curl http://localhost:5000/api/vaults/<vault-id>/assignments \
  -H "Authorization: Bearer $TOKEN"

# Update assignments (replace entire list)
curl -X PUT http://localhost:5000/api/vaults/<vault-id>/assignments \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "userIds": ["<user-uuid-1>", "<user-uuid-2>"],
    "groupIds": ["<group-uuid-1>"]
  }'
```

### Issue a session ticket

To programmatically launch a remote session:

```bash
# Step 1: Get a ticket
curl -X POST http://localhost:5000/api/guacamole/ticket/<connection-id> \
  -H "Authorization: Bearer $TOKEN"
# Response: { "ticket": "<one-time-ticket>" }

# Step 2: Open WebSocket (in browser or ws client)
# ws://localhost:5000/api/guacamole/connect/<connection-id>?ticket=<ticket>
```

### Query audit logs

```bash
# Last 50 login events
curl "http://localhost:5000/api/audit-logs?action=UserLogin&pageSize=50" \
  -H "Authorization: Bearer $TOKEN"

# Export all logs as CSV
curl "http://localhost:5000/api/audit-logs/export" \
  -H "Authorization: Bearer $TOKEN" \
  -o audit-export.csv
```

---

## TypeScript SDK pattern

For TypeScript projects, a simple wrapper class keeps your integration clean:

```typescript
class SmoothOperatorClient {
  private token: string | null = null;

  constructor(private baseUrl: string) {}

  async login(email: string, password: string): Promise<void> {
    const res = await fetch(`${this.baseUrl}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });
    const data = await res.json();
    this.token = data.token;
  }

  private async request<T>(method: string, path: string, body?: unknown): Promise<T> {
    const res = await fetch(`${this.baseUrl}/api${path}`, {
      method,
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${this.token}`,
      },
      body: body ? JSON.stringify(body) : undefined,
    });
    if (!res.ok) throw new Error(`${method} ${path} → ${res.status}`);
    return res.json() as Promise<T>;
  }

  getConnections() { return this.request<Connection[]>('GET', '/connections'); }
  getUsers()       { return this.request<User[]>('GET', '/users'); }
  getAuditLogs()   { return this.request<AuditLog[]>('GET', '/audit-logs'); }
}

// Usage
const client = new SmoothOperatorClient('http://localhost:5000');
await client.login('admin@example.com', 'password');
const connections = await client.getConnections();
```

---

## Next steps

- [Interactive Swagger UI](./swagger) — explore all endpoints visually
- [API Overview](./) — error codes, pagination, rate limits
