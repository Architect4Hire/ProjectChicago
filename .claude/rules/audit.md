---
paths:
  - "src/<api-project>/Modules/Audit/**/*.cs"
  - "src/<api-project>/Modules/**/Audit*.cs"
---
# Audit rules

- Audit security changes, lifecycle transitions, ownership changes, exports, merges, deletes/restores, and changes to configurable reference data.
- Record actor, action, target, timestamp, correlation ID, source, and a safe structured before/after summary.
- Do not store access tokens, passwords, full email bodies, or unrestricted free-form customer notes in audit payloads.
- Audit writes participate in the same transaction as the business change when practical.
- Audit records are append-only through product code.
