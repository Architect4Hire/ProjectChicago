# ADR-0007 — ASP.NET Core Identity Baseline

- **Status:** Accepted
- **Requirements:** SEC-001..016

## Context
Project Chicago needs production-grade account, credential, role and lockout primitives without custom password security.

## Decision
Use Microsoft ASP.NET Core Identity for application users, credential hashing, roles and account security primitives. Never implement custom password hashing/storage.

Initial product roles are Administrator, Manager, Contributor and ReadOnly. Server-side authorization is mandatory.

This ADR does **not** choose the browser cookie/token/session transport; that remains ADR-0018.

## Consequences
- Identity storage belongs to the bounded service selected for Identity ownership.
- Identity events must be auditable without storing credentials/tokens.
- Role/policy authorization is enforced at backend boundaries.
- UI role checks are affordances, not security.

## Alternatives considered
- Custom user/password store: rejected.
- Client-side-only authorization: rejected.

## Validation
Identity integration tests cover managers/stores, login failures/lockout where configured, and role-policy behavior.
