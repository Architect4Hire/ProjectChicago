# Authentication and Identity Propagation

ASP.NET Core Identity owns user/credential/role primitives. The browser transport is a separate approved decision.

## Trust boundary
Only claims from the approved authenticated context are trusted. Never accept user/role identity from arbitrary caller-supplied headers without a mechanism that strips/replaces/protects them at the gateway boundary.

## Downstream services
Each service independently enforces authorization using the trusted identity context. Resource-level checks happen at Facade/use-case boundary.

## Async actor context
Integration events may carry the originating actor identifier/type for audit, but that metadata is not a substitute for authenticating a new interactive request.

## Security
Never log/session-store secrets outside approved framework mechanisms. Distinguish 401 from 403.

See ADR-0018 before implementing cookie/token/session details.
