# ADR-0018: Browser Authentication and Session Transport

**Status:** Superseded  
**Date:** 2026-08-12  
**Superseded Date:** 2026-08-15  
**References:** SEC-001..025, ADR-0015 (Identity Service), identity.md, gateway.md, security.md  
**Superseding Decision:** Backend-for-Frontend (BFF) pattern with server-side session storage (2026-08-15)

## Decision (Superseded: 2026-08-12)

~~Project Chicago shall use **ASP.NET Core Cookie Authentication** for browser authentication and session transport.~~

---

## New Decision (Superseding: 2026-08-15)

Project Chicago shall use a **Backend-for-Frontend (BFF) pattern with server-side JWT token storage in Redis**. The YARP gateway acts as a credential-exchange boundary: it authenticates with Identity on the browser's behalf, stores JWT access and refresh tokens server-side in Redis, and issues an opaque HttpOnly session cookie to the browser. The browser never directly handles JWT tokens in any form (no response bodies, no storage, no headers). CRM and Audit services validate bearer tokens injected by the Gateway's BearerTokenMiddleware.

---

## Problem

The bounded-service catalog (ADR-0015) confirms the Identity Service owns ASP.NET Core Identity, but does not specify how the React browser application behind YARP authenticates users or maintains session state.

The project must decide:
- How does React obtain authentication (cookie, token, hybrid)?
- Where is session state stored?
- How is CSRF prevented?
- How are tokens/sessions revoked?
- How does authorization context flow across services?
- What are the 401 vs 403 semantics?

## Forces

- **SPA behind gateway** [ADR-0015]: React is single-origin, behind YARP; all backend access through YARP
- **ASP.NET Core Identity confirmed** [SEC-001]: Identity framework is locked in; use its native security primitives
- **CSRF protection required** [SEC-023]: Protect against cross-site request forgery
- **No credential exposure** [SEC-024]: Never log or expose passwords, tokens, or secrets
- **Clear authorization semantics** [SEC-010/012]: 401 (no auth) vs 403 (forbidden) must be distinct
- **Revocation required**: Logout must immediately invalidate sessions
- **Production-suitable**: Must work at scale without complex distributed session management

---

## Superseding Solution: Backend-for-Frontend (BFF) with Server-Side Session Storage (2026-08-15)

**Why we superseded:** The original cookie-auth design (2026-08-12) expected CRM and Audit to validate cookies issued by Identity, but no shared ASP.NET Data Protection key ring was established between processes. The BFF pattern eliminates this: Identity issues JWT bearer tokens, the Gateway stores them server-side in Redis, and all downstream services validate the same bearer tokens injected by the Gateway. This achieves stronger isolation. Additionally, the browser never handles JWT tokens in any form (no response bodies, no storage)—only an opaque HttpOnly session cookie is sent to the browser.

### Architecture

```
React (SPA)
    ↓ (POST /auth/login via YARP)
YARP Gateway (BFF: credential exchange boundary)
    ├─ Identity Service (validate credentials, mint JWT access + refresh tokens)
    ├─ Redis session store (session:{sessionId} = GatewaySession with tokens, user info, TTL)
    ├─ Browser (200 + .ProjectChicago.SessionId cookie + X-CSRF-TOKEN header)
    │
    └─ Proxy (BearerTokenMiddleware injects Authorization: Bearer header + CsrfValidationMiddleware validates X-CSRF-TOKEN)
        └─ CRM/Audit/Identity (validate incoming bearer tokens)
```

### Key Design Points

1. **Browser never sees raw JWT tokens** — Only opaque HttpOnly session cookie `.ProjectChicago.SessionId` (256-bit random ID)
2. **Server-side token storage** — JWT access/refresh tokens stored in Redis at `session:{sessionId}`; TTL = refresh token lifetime
3. **Inline token refresh** — `BearerTokenMiddleware` checks if access token expires within 60s; if yes, calls Identity `/auth/refresh`, rotates tokens, updates Redis, injects new token
4. **Bearer token injection** — Only Gateway injects Authorization headers; downstream services validate JWT signature/issuer/audience/lifetime using same signing key
5. **CSRF via double-submit** — Gateway issues CSRF token on login (header `X-CSRF-TOKEN`); React captures it; attached to all mutations; validated by `CsrfValidationMiddleware`
6. **Endpoint routing** — `/auth/login` and `/auth/logout` handled by Gateway; all other `/auth/*` and `/api/**` routes proxied with bearer injection

### Configuration Summary

- **Identity:** JWT issuance (`JwtTokenService`), `/auth/refresh` endpoint, `Jwt__SigningKey` from environment
- **Gateway:** Redis session store (`ISessionStore`), `BearerTokenMiddleware`, `CsrfValidationMiddleware`, `AuthEndpoints` (login/logout), JWT config from appsettings
- **CRM/Audit:** JWT bearer validation (`AddJwtBearer` with same `Jwt__SigningKey`), JWT config from appsettings
- **React:** CSRF token capture (`http.ts`), CSRF header attachment on mutations, `setCsrfToken()` on login

---

## Original Solution: ASP.NET Core Cookie Authentication (Superseded 2026-08-15)

### Architecture

```
React (SPA)
    ↓
YARP Gateway (extracts ClaimsPrincipal from cookie)
    ↓
Identity Service (validates credentials, issues cookie)
    ↓
Downstream Services (receive trusted ClaimsPrincipal via ICurrentUser)
```

### Session Token Storage

- **Transport:** HTTPOnly, Secure, SameSite=Strict cookie set by `CookieAuthenticationHandler`
- **Browser behavior:** Automatically included in all same-origin requests to YARP
- **Backend storage:** Session state in distributed cache (SqlServer or Redis configured in Aspire)
- **Cookie name:** `.ProjectChicago.Session` (configurable)
- **Flags enforced:**
  - `HttpOnly=true` → prevents JavaScript access (XSS protection)
  - `Secure=true` → only sent over HTTPS
  - `SameSite=Strict` → not sent cross-site (CSRF protection)
  - `IsEssential=true` → cookie persists without user consent

### CSRF Protection

- **Mechanism:** AntiForgery token middleware on Identity Service
- **Token lifetime:** Per session; new token on login
- **Client behavior:** React extracts token from response header (`X-CSRF-TOKEN`) after login; stores in memory (not localStorage)
- **Mutation behavior:** Every POST/PUT/PATCH/DELETE includes token in `X-CSRF-TOKEN` request header
- **Validation:** Each service validates token per request (not cached)
- **Token format:** Encrypted, anti-tampering (built-in ASP.NET Core AntiForgery)

### Session Lifecycle

| Operation | Behavior | Status |
|-----------|----------|--------|
| **Login** | POST credentials → Identity Service validates → issues session cookie + AntiForgery token | 200 (success) or 401 (invalid) or 429 (locked) |
| **Authenticated request** | Browser sends cookie → YARP extracts ClaimsPrincipal from session → forwards to service | 200/403 (authorized/forbidden) or 401 (expired) |
| **Logout** | POST logout → Identity Service clears session state → browser discards cookie | 200 |
| **Session expiry** | Idle timeout (default 30 min) or absolute timeout (default 8 hr) → session garbage collected → next request 401 | 401 (not authenticated) |
| **Password reset** | Issues temporary token → invalidates old session → user must re-login | 200 |
| **Multi-tab logout** | Logout in tab A → session cleared → tab B sees 401 on next request (no sync needed) | 401 |

### Authorization Context Flow

```
YARP Middleware:
  1. Extract cookie
  2. Retrieve session from distributed store
  3. Deserialize ClaimsPrincipal
  4. Validate security stamp (user not tampered)
  5. Set HttpContext.User

Controller:
  1. Receive HttpContext.User (via ICurrentUser abstraction)
  2. Call Facade with authenticated context
  3. Facade validates authorization policy (service-owned)

Service Authorization:
  - Each service evaluates authorization against resource/action
  - Do NOT trust user/role/tenant headers from browser
  - Only trust ClaimsPrincipal deserialized from validated session state
```

### HTTPS and Secure Flags

- All authentication endpoints (login, logout, account endpoints) must use HTTPS
- Cookie issued with `Secure` flag (only sent over HTTPS)
- Cookie issued with `HttpOnly` flag (never accessible to JavaScript)
- Cookie issued with `SameSite=Strict` (no cross-site cookie send)
- Development: use `https://localhost:443` with self-signed cert in Aspire
- Production: enforced by HTTPS listener and `RequireHttpsMetadata=true`

### Error Responses and Semantics

| Scenario | Status | Response |
|----------|--------|----------|
| Valid session, action authorized | 200/201/204 | Success |
| Valid session, action unauthorized | 403 Forbidden | ProblemDetails: "User lacks required role/claim" |
| No session or session expired | 401 Unauthorized | ProblemDetails: "Authentication required" |
| Invalid credentials | 401 Unauthorized | ProblemDetails: "Invalid username/password" |
| Account locked | 429 Too Many Requests | ProblemDetails: "Account locked after N failed attempts" |
| AntiForgery token missing/invalid | 400 Bad Request | ProblemDetails: "Invalid CSRF token" or 403 (service choice) |

---

## Security Invariants

1. **No credential exposure in logs** — Passwords never logged; session tokens never exposed in audit/telemetry
2. **No JavaScript access to session cookie** — HTTPOnly flag prevents XSS from stealing token
3. **CSRF protection on mutations** — AntiForgery token required for state-changing operations
4. **Session revocation is immediate** — Logout clears session state; next request returns 401
5. **Multi-service authorization** — Trusted ClaimsPrincipal flows via ICurrentUser; each service validates resource-level authorization
6. **Clear authentication semantics** — 401 = no valid session; 403 = session valid but action forbidden
7. **No header spoofing** — User/role/tenant headers from browser are untrusted; only parsed from validated session state
8. **HTTPS enforcement** — Secure flag prevents cookie send over HTTP; Strict-Transport-Security header recommended

---

## Testing Strategy

### Unit/Integration Tests (Backend)

- [ ] **Login success:** Valid credentials → 200, session created, AntiForgery token issued
- [ ] **Login failure:** Invalid credentials → 401, no session created
- [ ] **Account locked:** After N failed attempts → 429, account locked, no session
- [ ] **Logout:** Valid session → 200, session cleared, cookie removed
- [ ] **Protected endpoint (authenticated):** Valid session + authorized → 200
- [ ] **Protected endpoint (unauthorized):** Valid session + insufficient claims → 403
- [ ] **Protected endpoint (unauthenticated):** No session → 401
- [ ] **AntiForgery (valid token):** Mutation with valid token → 200/201/204
- [ ] **AntiForgery (missing token):** Mutation without token → 400/403
- [ ] **AntiForgery (invalid token):** Mutation with tampered token → 400/403
- [ ] **Session expiry:** Request after idle timeout → 401
- [ ] **Password reset:** Changes password → old session invalidated, user must re-login
- [ ] **Concurrent requests:** Multiple simultaneous requests with same session → session state consistent
- [ ] **Multi-tab logout:** Logout in one tab → other tabs see 401 on next request

### Security Tests (Automated/Manual)

- [ ] **XSS test:** `document.cookie` in browser console does not reveal session value (HttpOnly flag)
- [ ] **CSRF test:** POST/PUT/PATCH without AntiForgery token → 400/403
- [ ] **HTTPS test:** Session cookie marked Secure; never sent over HTTP (test with https://localhost)
- [ ] **Cookie scope test:** Cookie not sent to external domains (SameSite=Strict)
- [ ] **Authorization bypass:** Unauthenticated user cannot access protected endpoint via header spoofing (user/role headers ignored)
- [ ] **Session hijacking:** Session cookie cannot be replayed across different browsers (tied to IP/User-Agent when feasible)
- [ ] **Session fixation:** Cannot force user to use attacker's session ID

### React Component Tests

- [ ] **Login form:** Submit credentials → receive 200 + AntiForgery token → store token in memory
- [ ] **Login error:** Submit invalid credentials → receive 401 → display error message
- [ ] **Login lockout:** Multiple failed attempts → receive 429 → display lockout message + unlock instructions
- [ ] **Logout button:** Click logout → call logout endpoint → clear local state → redirect to login
- [ ] **Protected route redirect:** Unauthenticated user accesses protected route → redirect to login
- [ ] **Forbidden message:** Authenticated but unauthorized user → display "Access denied" message
- [ ] **AntiForgery header:** Mutation requests include `X-CSRF-TOKEN` header with token value
- [ ] **Session loss:** Close browser/clear cookies → next protected request → 401 → redirect to login
- [ ] **Multi-tab sync:** Logout in one tab → open page in another tab → see 401 → redirect to login

---

## Rejected Alternatives

### Option 2: Bearer Token (JWT in localStorage)

**Why rejected:**
- XSS vulnerability: If JavaScript is compromised, token in localStorage is exposed
- No CSRF protection (token in header, not cookie) — requires separate CSRF mitigation
- Complex token refresh logic: requires `/auth/refresh` endpoint or background token rotation
- localStorage is not suitable for authentication secrets (persistent, readable by JavaScript)
- Requires custom logout logic (no automatic cleanup on tab close)

**When this might be reconsidered:**
- If external/mobile clients access the API in the future (but refresh tokens in httpOnly cookies are still preferred)
- If stateless API architecture is required (but cookie-based sessions can use distributed cache, which is still scalable)

### Option 3: Hybrid (Short-Lived Cookie + Refresh Token)

**Why rejected:**
- More complex than needed for a single-origin SPA
- Requires additional `/auth/refresh` endpoint and refresh token management
- Cookie-based session with idle timeout is simpler and equally secure
- Adds operational overhead (refresh token rotation, revocation, etc.)

**When this might be reconsidered:**
- If compliance requires short-lived access tokens (e.g., 5-min lifetime, refresh every 5 min)
- If token revocation latency must be < 5 minutes (but session immediate revocation satisfies most use cases)

---

## Configuration and Implementation Checklist

### ASP.NET Core Setup (Identity Service)

- [ ] Add `services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options => { options.LoginPath = "/auth/login"; options.LogoutPath = "/auth/logout"; options.ExpireTimeSpan = TimeSpan.FromMinutes(30); options.SlidingExpiration = true; })` to Startup/Program
- [ ] Configure cookie: `CookieHttpOnlyFlag=true`, `CookieSecurePolicy=CookieSecurePolicy.Always`, `CookieSameSite=SameSiteMode.Strict`
- [ ] Add `services.AddAntiforgery(options => { options.HeaderName = "X-CSRF-TOKEN"; })` to Startup
- [ ] Configure distributed cache for session state (SqlServer or Redis in Aspire)
- [ ] Implement login endpoint: validate Identity credentials → issue session cookie + return AntiForgery token
- [ ] Implement logout endpoint: clear session, sign out (automatic cookie discard)
- [ ] Implement password reset/change endpoints with audit logging (never log credentials)

### YARP Gateway Setup

- [ ] Add cookie authentication middleware to accept incoming cookies from React
- [ ] Extract ClaimsPrincipal from session and set as HttpContext.User
- [ ] Pass ClaimsPrincipal to downstream services via ICurrentUser (not header injection)
- [ ] Add `HttpsRedirection` middleware for HTTPS enforcement
- [ ] Add security headers: `Strict-Transport-Security`, `X-Content-Type-Options`, `X-Frame-Options`

### React Client Setup

- [ ] Create login form component using PCDS
- [ ] POST credentials to `/auth/login` endpoint via gateway
- [ ] Extract AntiForgery token from response (from header or JSON body)
- [ ] Store token in component state/memory (NOT localStorage)
- [ ] Include token in all mutation request headers: `{ "X-CSRF-TOKEN": token }`
- [ ] Handle 401: clear state, redirect to login
- [ ] Handle 403: display "Access denied" message
- [ ] Create logout button: POST to `/auth/logout` → clear state → redirect to login
- [ ] Protect routes: unauthenticated redirect to login

### Testing Setup

- [ ] Unit tests for login/logout endpoints with credential validation
- [ ] Integration tests for protected endpoints (authenticated/unauthorized/unauthenticated)
- [ ] AntiForgery token validation tests (valid/missing/invalid tokens)
- [ ] React component tests for login form, logout button, protected route redirect
- [ ] Security tests: XSS (cookie not accessible to JS), CSRF (token required), HTTPS enforcement
- [ ] Audit logging tests: login/logout/password reset events logged without credential exposure

### Aspire Configuration

- [ ] Distributed cache resource (SqlServer or Redis) configured
- [ ] Identity Service HTTP host registered
- [ ] HTTPS endpoint configured for local development (self-signed cert)
- [ ] React app configured to communicate with YARP (not internal services)

---

## Deployment Considerations

### Local Development (Aspire)

- Use `https://localhost:5001` or similar with self-signed certificate
- `RequireHttpsMetadata=true` in development (test with HTTPS)
- Distributed cache in SqlServer (simpler than Redis for local dev)

### Production (Azure)

- HTTPS enforced by App Service/Container Instance listener
- `RequireHttpsMetadata=true`
- Session cache in Azure Cache for Redis or SqlServer (managed)
- Strict-Transport-Security header for HSTS
- Cookie domain/SameSite policy reviewed per environment

---

## Future Evolution

- **Token Rotation:** If compliance requires sub-5-minute token lifetime, implement access token + refresh token pattern (both in httpOnly cookies)
- **External APIs:** If mobile or external apps access the API, add a separate JWT bearer token endpoint (but keep cookie auth for browser)
- **MFA/Passkeys:** If MFA is added, integrate with ASP.NET Core Identity 2FA support; no change to cookie/CSRF approach
- **Rate Limiting:** Add rate limiting to login endpoint to prevent brute force (per SEC-016)

---

## Acceptance Criteria

- [ ] ASP.NET Core cookie authentication middleware configurable in Aspire
- [ ] AntiForgery protection middleware registered and token endpoints working
- [ ] React login form successfully authenticates and receives session cookie + AntiForgery token
- [ ] Protected API endpoint requires valid session cookie; 401 without cookie
- [ ] 401 vs 403 semantics verified (401 = no session, 403 = session but unauthorized)
- [ ] Logout endpoint clears session and discards cookie immediately
- [ ] Multi-tab logout verified (tab B sees 401 after tab A logout)
- [ ] Security tests passing (XSS, CSRF, HTTPS enforcement)
- [ ] React component tests passing (login, logout, protected route behavior)
- [ ] Audit logging covers login/logout/password reset events (no credential logging)
- [ ] Documentation updated with cookie/CSRF configuration details

---

## References

- [SEC-001..025: Security Requirements](../requirements/lightweight-crm-product-and-system-requirements.md#sec-001)
- [ADR-0015: Bounded-Context Catalog](adr-0015-bounded-context-catalog.md)
- [ASP.NET Core Cookie Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie)
- [ASP.NET Core Antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery)
- [OWASP: CSRF Prevention](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html)
- [OWASP: Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
- [.claude/rules/identity.md](../../.claude/rules/identity.md)
- [.claude/rules/gateway.md](../../.claude/rules/gateway.md)
- [.claude/rules/security.md](../../.claude/rules/security.md)
