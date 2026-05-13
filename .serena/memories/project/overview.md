# Project Overview: Smooth Operator 🕶️ 🔐

## Purpose
**Smooth Operator** is a cloud-native, clientless remote access vault. It provides secure, browser-based access to SSH, RDP, and VNC servers without requiring VPN clients or exposing direct ports.

## Tech Stack
- **Frontend**: Angular 21, Tailwind CSS 4, guacamole-common-js (for remote protocol rendering).
- **Backend**: .NET 10 (ASP.NET Core), Clean Architecture (Domain, Application, Infrastructure, Api), CQRS with MediatR.
- **Database**: PostgreSQL 15.
- **Cache**: Redis 7.
- **Connection Engine**: Apache guacd 1.6.
- **Docs**: Docusaurus 3.
- **Testing**: xUnit/FluentAssertions (Backend), Vitest/Angular Testing Library (Frontend), Playwright (E2E).

## Key Features
- RBAC with four built-in roles.
- Vault-based isolation.
- SSO via OIDC and SAML 2.0.
- MFA and audit logs.
