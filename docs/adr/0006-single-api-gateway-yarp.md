# ADR-0006 — YARP as the Single Browser-Facing Backend Edge

- **Status:** Accepted
- **Requirements:** SEC-020..025, API-001..007

## Context
Exposing every service directly to the browser multiplies networking, security, CORS, domain and operational concerns.

## Decision
YARP is Project Chicago's only browser-facing backend edge. The React application calls stable gateway routes and never internal service addresses. Services and Functions are not independent public browser APIs.

The gateway handles edge routing and cross-cutting edge concerns; it does not own CRM business logic.

## Consequences
- Public route stability is decoupled from internal service discovery.
- Auth/session handling must be designed with the gateway boundary.
- Service addresses remain internal.
- Gateway must stay persistence/broker-free.

## Alternatives considered
- Public domain per service: rejected due to operational and security surface.
- API Management as an additional gateway: not part of the initial architecture.
- Browser calling Functions: explicitly rejected.

## Validation
Gateway/API tests prove routing; architecture/security review checks the React code has no internal service URL.
