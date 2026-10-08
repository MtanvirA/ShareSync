# ShareSync — Security Architecture & Hardening (LF-32)

## 1. Authentication & Password Security
- **Hashing Standard:** PBKDF2 with SHA-256 (`Rfc2898DeriveBytes.Pbkdf2`), 100,000 iterations, 128-bit cryptographically secure salt, 256-bit derived key.
- **Verification:** Constant-time comparison (`CryptographicOperations.FixedTimeEquals`) to prevent timing side-channel attacks.
- **Password Rules:**
  - Enforced minimum length (minimum 6 characters, recommended 8+).
  - Confirmation match required.
  - Rejection of identical passwords upon password change (`New password must be different from current password`).
- **No Plaintext Storage:** Passwords and credentials are never stored, transmitted in logs, or returned in API responses.

---

## 2. Token & Session Management
- **JWT Authentication:** HMAC-SHA256 (`HmacSha256`) signed tokens with claims for `UserId`, `Email`, and `Role`.
- **Validation:** Strict validation of issuer (`ValidIssuer`), audience (`ValidAudience`), lifetime (`ValidateLifetime`), and `ClockSkew = TimeSpan.Zero`.
- **Stateless Logout:** Client-side token purge supplemented with an explicit `/api/auth/logout` endpoint.

---

## 3. Account Isolation & Authorization (Anti-IDOR)
- **Account Isolation:** All user-scoped resources (portfolios, transactions, holdings, watchlists, dividends, alerts, goals, notifications, snapshots, reports) enforce ownership checks:
  - If a resource belongs to another user, requests are rejected with `403 Forbidden` (`AppException("You do not have permission...", 403)`).
  - Changing an ID in an API request or URL parameter does not grant cross-tenant access.
- **Role-Based Access Control:** Administrative endpoints are guarded with `[Authorize(Roles = "ADMIN")]`.

---

## 4. Multi-Factor Authentication (MFA Foundation)
- **Engine:** RFC 6238 Time-Based One-Time Password (TOTP) algorithm using HMAC-SHA1.
- **Parameters:** 30-second timestep, 6 digits, RFC 4648 Base32-encoded secret keys.
- **Drift Tolerance:** Validates ±1 timestep (±30s) to handle network latency and slight client clock drift.
- **Endpoints:**
  - `GET /api/auth/mfa/status` — Returns technical readiness and algorithm details.
  - `POST /api/auth/mfa/generate` — Generates a setup secret key and standard `otpauth://` URI.
  - `POST /api/auth/mfa/verify` — Validates candidate 6-digit codes.
- **Scope Notice:** Cryptographic key generation and code validation algorithms are active. Mandatory enrollment prompt on every login is scheduled for future enterprise release.

---

## 5. Input Security & Data Protection
- **SQL Injection Prevention:** 100% of application queries use Entity Framework Core parameterized LINQ queries. No dynamic string concatenation in SQL commands.
- **XSS Prevention:** Frontend sanitizes dynamic content via universal `escapeHtml()` utility before rendering into the DOM.
- **Information Leakage Prevention:** Database constraint errors and connectivity issues are sanitized by `ExceptionHandlingMiddleware` and `HealthController` to prevent disclosure of connection strings, database hosts, or internal queries.
- **CORS Policy:** Restricts allowed origins to configured hosts in production and loopback in development.
