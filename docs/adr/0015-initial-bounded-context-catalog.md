# ADR-0015 — Initial Bounded-Context Catalog

- **Status:** Proposed
- **Requirements:** PR-001..006, DATA-031..034, SEC-001..016, AUDIT-001..008

## Context
The product scope is intentionally small: Clients, Projects and Tasks plus identity and durable auditing. The repository architecture requires explicit bounded-service ownership before scaffolding service projects.

## Proposed decision
Start with three bounded contexts:

1. **Crm** — owns Clients, Projects and Tasks and all business rules around their relationships, lifecycle/status, assignment and dashboard/search read behavior.
2. **Identity** — owns ASP.NET Core Identity users, roles and authentication/account operations.
3. **Audit** — owns the append-only durable business audit trail and authorized audit queries.

Each bounded context has one HTTP host, one `.Core`, one `.Functions` sibling and exactly one SQL database.

Clients/Projects/Tasks stay together initially because their relationship is tight and the product is explicitly lightweight. Splitting each entity into a service would create distribution overhead without a current business boundary.

## Consequences if accepted
- `CrmDb`, `IdentityDb`, `AuditDb`.
- Audit is fed asynchronously; Crm never writes AuditDb.
- Search/dashboard stay in Crm while their data remains CRM-owned.
- New services require a later ADR and business justification.

## Alternatives considered
- Client/Project/Task as separate microservices: rejected as premature distribution.
- One monolithic API/database: conflicts with established distributed service shape.
- Audit tables in each service only: does not provide the requested centralized durable support trail.

## Validation
Accept only after architecture review. Prompt 002 proposes the catalog; Prompt 003 records the approved decision; Prompt 004 updates `CLAUDE.md`.
