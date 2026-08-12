# ADR-0018 — Browser Authentication and Session Transport

- **Status:** Proposed
- **Requirements:** SEC-001..025

## Context
ASP.NET Core Identity is fixed, but a React SPA behind YARP still needs an explicit choice for browser credential/session transport. The decision affects CSRF, token storage, revocation, gateway behavior and downstream authorization context.

## Proposed decision
No transport is accepted in this ADR yet.

The architecture review must compare at minimum:

- secure HttpOnly cookie/session approaches,
- access-token approaches and where tokens are stored,
- whether YARP acts as a BFF-style session boundary,
- CSRF protection,
- revocation/logout,
- multi-service propagation of trusted identity/claims,
- 401 vs 403 behavior,
- refresh/lifetime policy.

A production choice must avoid exposing long-lived credentials to insecure browser storage.

## Consequences
Downstream auth endpoint and gateway design is blocked until this ADR is Accepted.

## Validation
Use the decision prompt in the canonical SCRUB sequence; record the approved choice here or supersede this placeholder ADR.
