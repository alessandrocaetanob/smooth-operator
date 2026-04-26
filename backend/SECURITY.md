# Security Implementation Guide

This document outlines the security features implemented in the Smooth Operator backend and provides guidance for secure deployment.

## Security Features

### 1. Authentication & Authorization

- **JWT-based Authentication**: Local HS256 JWT is the default auth flow; Azure Entra ID is optional.
- **Role-Based Access Control**: Four default roles are enforced: `Owner`, `Admin`, `TeamAdmin`, and `User`.
- **Connection Permission Checks**: Users can only access connections explicitly assigned to them or within assigned vaults.
- **Active User Validation**: Inactive user accounts cannot authenticate or access resources

### 2. Audit Logging

Comprehensive audit logging tracks all security-relevant events:

- `user.provisioned` - New user created via JIT provisioning
- `user.invited` - Admin invited a new user
- `connection.started` - User initiated a connection session
- `connection.ended` - Connection session terminated
- `connection.failed` - Connection attempt failed (not found)
- `connection.unauthorized` - User attempted to access unauthorized connection
- `connection.error` - Error occurred during connection session

All audit logs include:
- User ID
- Timestamp
- Action type
- Resource type and ID
- IP address
- Additional details (JSON)

### 3. Encryption

- **AES-256 Encryption**: All credentials stored in database are encrypted
- **Encryption Key**: Must be 64-character hex string (32 bytes)
- **Per-credential IV**: Each encrypted credential uses unique initialization vector

### 4. Input Validation

All API endpoints validate input using Data Annotations:
- Email format validation
- String length constraints
- Required field validation
- Protocol/type enumeration validation
- JSON structure validation

### 5. Rate Limiting

- **100 requests per minute** per authenticated user
- Prevents brute force and DoS attacks
- Configurable in `Program.cs`

### 6. Health Checks

Health check endpoint at `/health` monitors:
- PostgreSQL database connectivity
- Redis connectivity

## Configuration Requirements

### Required Environment Variables

```bash
# Database Connection
ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=smoothoperator;Username=postgres;Password=<secure-password>

# Redis Connection
ConnectionStrings__Redis=redis:6379

# Guacamole Daemon
Guacd__Host=guacd
Guacd__Port=4822

# Encryption Key (64 hex characters = 32 bytes)
# Generate with: openssl rand -hex 32
ENCRYPTION_KEY=<64-character-hex-string>

# Frontend URL (for invite links)
FRONTEND_URL=https://your-frontend-url.com

# Azure Entra ID Configuration
AzureAd__Instance=https://login.microsoftonline.com/
AzureAd__Domain=<your-domain>.onmicrosoft.com
AzureAd__TenantId=<your-tenant-id>
AzureAd__ClientId=<your-client-id>
```

### Production Deployment Checklist

- [ ] **Never use hardcoded encryption keys**
  - Use Azure Key Vault or similar secrets management
  - Rotate encryption keys regularly

- [ ] **Secure Database Credentials**
  - Use managed identities when possible
  - Never commit credentials to source control

- [ ] **Enable HTTPS Only**
  - Configure SSL/TLS certificates
  - Disable HTTP in production

- [ ] **Configure CORS Properly**
  - Limit allowed origins to your frontend domain
  - Never use wildcard (`*`) in production

- [ ] **Review Audit Logs Regularly**
  - Set up alerts for suspicious activity
  - Monitor for unauthorized access attempts

- [ ] **Implement Role-Based Access Control (RBAC)**
  - Define admin, operator, and viewer roles
  - Implement authorization policies

- [ ] **Configure Rate Limiting**
  - Adjust limits based on expected usage
  - Consider per-endpoint limits for sensitive operations

- [ ] **Enable Additional Security Headers**
  - Content-Security-Policy
  - X-Frame-Options
  - X-Content-Type-Options
  - Strict-Transport-Security

## API Security Best Practices

### Credential Management

1. **Never return encrypted secrets** in API responses
2. **Use Update endpoint** to rotate credentials
3. **Delete old credentials** when no longer needed
4. **Audit credential access** in application logs

### Connection Security

1. **Verify user permissions** before allowing connections
2. **Log all connection attempts** (successful and failed)
3. **Implement session timeouts** for inactive connections
4. **Monitor concurrent sessions** per user

### User Management

1. **Validate email addresses** on user creation
2. **Implement secure invite tokens** (time-limited)
3. **Support user deactivation** instead of deletion
4. **Track last login times** for security audits

## Threat Model

### Mitigated Threats

- ✅ Unauthorized access to connections
- ✅ Credential theft from database
- ✅ User enumeration attacks
- ✅ Brute force authentication
- ✅ SQL injection (via parameterized queries)
- ✅ Over-posting attacks (via DTOs)

### Potential Risks (Require Additional Mitigation)

- ⚠️ Compromised encryption key (use key rotation)
- ⚠️ Man-in-the-middle attacks (enforce HTTPS)
- ⚠️ Session hijacking (implement session tokens)
- ⚠️ Insider threats (implement RBAC and audit review)
- ⚠️ Denial of service (implement advanced rate limiting)

## Incident Response

If a security incident occurs:

1. **Immediately rotate encryption keys**
2. **Review audit logs** for compromised accounts
3. **Force password/credential resets** for affected users
4. **Notify stakeholders** per your incident response plan
5. **Document the incident** and lessons learned

## Compliance Notes

This implementation provides foundational security controls. Additional measures may be required for:

- GDPR compliance (data retention, right to deletion)
- HIPAA compliance (PHI handling, BAAs)
- SOC 2 compliance (access controls, monitoring)
- ISO 27001 compliance (information security management)

Consult with your compliance team to ensure all requirements are met.
