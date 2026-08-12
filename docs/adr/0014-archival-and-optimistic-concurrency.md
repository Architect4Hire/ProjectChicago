# ADR-0014 — Archival and Optimistic Concurrency for CRM Records

- **Status:** Accepted
- **Requirements:** DATA-008, DATA-020..023, CLIENT-013..015, PROJECT-014

## Context
CRM records carry historical/audit value and can be edited concurrently. Silent overwrite and routine hard deletion undermine auditability.

## Decision
Normal workflows archive rather than physically delete Clients, Projects and Tasks where applicable. Mutable business records use an explicit optimistic concurrency mechanism. Stale updates return a conflict rather than silently overwriting newer data.

Permanent purge is a privileged retention/privacy operation, not ordinary CRUD.

## Consequences
- Default lists exclude archived data unless explicitly requested.
- Unique/index policies must account for archival semantics.
- APIs/UI must surface concurrency conflicts.
- Audit history survives archival.

## Validation
SQL/API tests prove stale conflicts, archive visibility, active-project Client restrictions and historical preservation.
