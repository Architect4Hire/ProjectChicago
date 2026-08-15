# ADR-0018 — Browser Authentication and Session Transport

- **Status:** Superseded
- **Requirements:** SEC-001..025
- **Superseded by:** ADR-0018 at `docs/design/adr-0018-browser-authentication-session.md` (2026-08-15)

## Context (Superseded)

This ADR was a placeholder for browser authentication transport choice. The decision has been made and documented in the canonical ADR-0018 file.

## Decision (Superseded 2026-08-15)

**This ADR document is superseded. See the canonical decision at `docs/design/adr-0018-browser-authentication-session.md`.**

The project uses a **Backend-for-Frontend (BFF) pattern with server-side JWT token storage in Redis**. The YARP gateway acts as a credential-exchange boundary: it authenticates with Identity on the browser's behalf, stores JWT tokens server-side in Redis, and issues an opaque HttpOnly session cookie to the browser. The browser never directly handles JWT tokens in any form.

For complete details, configuration, testing strategy, and rationale, refer to the canonical ADR-0018 document.
