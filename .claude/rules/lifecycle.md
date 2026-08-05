---
paths:
  - "src/<api-project>/Modules/Lifecycle/**/*.cs"
  - "src/web/src/app/features/lifecycle/**/*"
---
# Lifecycle rules

- All stage changes go through one lifecycle transition Business operation.
- Validate that the target stage is enabled and applicable to the entity type.
- Persist current stage and append transition history atomically.
- A transition must be idempotent when a client-supplied operation ID is repeated.
- Reordering or renaming stages must not rewrite historical transitions.
- Disabled stages remain visible in history and reporting but cannot receive new transitions.
- Dashboard funnel metrics use transition history for period analysis and current stage for snapshot analysis; never mix the two silently.
- Every transition appears on the customer activity timeline and in the audit log.
