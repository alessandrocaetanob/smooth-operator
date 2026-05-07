## 2025-05-07 - Missing Rate Limiting on Invite Preview
**Vulnerability:** The `[HttpGet("{token}")]` endpoint in `InvitesController` allowed unauthenticated users to validate invite tokens without rate limiting.
**Learning:** Even read-only or "preview" endpoints that expose the validity of sensitive tokens (like invites, password resets, or one-time codes) are vulnerable to enumeration and brute-force attacks if left unthrottled. The global rate limiter is often too permissive (e.g., 100 requests/min) to prevent targeted token discovery.
**Prevention:** Apply strict rate limiting (e.g., `[EnableRateLimiting("auth")]`) to any endpoint that validates, redeems, or checks the status of sensitive tokens, especially those accessible via `[AllowAnonymous]`.
