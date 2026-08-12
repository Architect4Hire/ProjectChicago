---
paths:
  - "src/ProjectChicago.Gateway/**"
  - "src/services/ProjectChicago.*/**"
  - "src/web/**"
  - "tests/**"
---
# ASP.NET Core Identity rules

Project Chicago uses **ASP.NET Core Identity** for application user identity. This decision defines the framework, but it does not yet define the bounded service that owns the Identity store or the browser session/token transport.

## Invariants

- Use supported ASP.NET Core Identity managers/stores/token providers/security stamp/lockout/password hashing behavior rather than custom credential code.
- Do not replace Identity with another provider or custom user table as an incidental implementation choice.
- Exactly one future bounded service/database owns the Identity schema. Until that owner is explicitly defined, do not invent an `Identity` bounded service.
- Other bounded services use authenticated actor identity/claims and their own service-owned references; they do not attach/query the Identity DbContext.
- YARP remains the only browser-facing edge. Authentication/account routes go through YARP to the eventual owning HTTP service.
- Azure Functions are not a user login surface and must not expose HTTP-triggered auth endpoints.

## Security decisions that remain explicit

Do not silently choose or redesign:

- secure cookie vs token/session transport;
- refresh/session lifetime and revocation;
- MFA/passkeys;
- email/phone confirmation;
- password/recovery policy;
- external login providers;
- tenant model and tenant/user claim semantics.

These choices must be documented as security/public-contract decisions when the authentication feature is designed.

## React

- Use local copied PCDS controls/patterns for login, registration, recovery and account-security UI.
- Never implement password hashing or credential validation rules in React.
- Do not persist passwords or long-lived secrets in localStorage/sessionStorage.
- Use the shared typed gateway client for auth/account requests.
- Treat 401 (not authenticated) and 403 (authenticated but forbidden) distinctly.

## Authorization

- Edge authentication does not replace service authorization.
- The service that owns a resource/action evaluates authorization against trusted authenticated context and service-owned state.
- Never trust user/role/tenant headers supplied directly by the browser without validated authentication context.

## Persistence

When Identity ownership is defined:

- its schema lives in that bounded service's one Microsoft SQL database;
- migrations stay with that owning implementation project;
- no other service joins to or directly queries Identity tables;
- secrets/connection strings remain in supported configuration/managed-secret mechanisms.

## Tests

Cover the security behavior selected by the project, including successful/failed authentication, lockout/recovery where enabled, authorization boundaries, 401 vs 403 behavior, logout/session invalidation, and protected-route behavior in React.
