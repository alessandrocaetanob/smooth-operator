## 2024-04-27
- Discovered that testing backend controllers requiring encryption depends on the `ENCRYPTION_KEY` environment variable being populated due to the `EncryptionService` constructor constraints.
- When creating test factories inheriting from `TestWebApplicationFactory` or testing endpoints that hit the database (which in turn uses `EncryptionService` for sensitive fields), we must ensure we explicitly configure `Environment.SetEnvironmentVariable("ENCRYPTION_KEY", new string('a', 64))` to avoid 500 errors during controller instantiation.
