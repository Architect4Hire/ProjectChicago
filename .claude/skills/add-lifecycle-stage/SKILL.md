---
name: add-lifecycle-stage
description: >
  Add, rename, reorder, disable, or alter one customer lifecycle stage while preserving stable identity,
  append-only transition history, concurrency, funnel/report semantics, UI tokens, authorization, auditing,
  and backwards compatibility.
---
# Add or change a lifecycle stage

Lifecycle stages are durable business data, not display strings. A stage change affects current customer
state, historical facts, transitions, reports, automation, filters, accessibility, and UI design tokens.
Treat it as a schema/contract-level change even when no database column changes.

## Discovery gate

Before changing code, discover the actual solution/project paths, namespaces, target frameworks, package versions, AppHost resource names, SQLDB connection name, DbContext, migrations assembly, test conventions, and feature location. Never treat example names as repository facts. Stop without editing when a required value cannot be proven. Aspire is required and is the supported source of local SQLDB connection information.

## Classify the request

Choose exactly one:

- Add a new stable stage.
- Rename display label only.
- Reorder funnel display.
- Disable future entry.
- Change allowed transitions.
- Change color/icon/description only.
- Merge/remove historical meaning — destructive and requires a migration plan, never a simple edit.

## Invariants

- Stage IDs/codes are stable and never reused.
- Historical transition rows are append-only.
- Current stage is derived/maintained consistently with history.
- One transition creates one history row and one audit fact.
- Concurrent transitions from the same expected stage cannot both succeed.
- Disabled stages remain resolvable for history and reporting.
- Display order is not identity.
- Color is not meaning.

## Procedure

### 1. Inventory all consumers

Search for stage ID/code/label usage in:

- Seed/configuration data.
- Domain transition rules.
- EF constraints/indexes.
- API contracts and validators.
- Automation/rules engine.
- Reporting/funnel queries.
- Dashboard metrics.
- Angular filters, steppers, badges, legends, and forms.
- Tests, fixtures, and snapshots.

Report the impact map before editing.

### 2. Define the stable stage specification

Document:

- Stable ID/code.
- Display label and description.
- Display order.
- Active/disabled status.
- Allowed predecessor/successor stages.
- Terminal/reopen behavior.
- Required permission.
- Required reason or metadata.
- Semantic design token and icon.
- Reporting bucket and conversion meaning.
- Effective date if semantics change over time.

### 3. Update configuration only

In this microstep, update the authoritative stage catalog/seed. Preserve old IDs. Do not rewrite transition
history. If seed data uses EF `HasData`, understand its migration implications; prefer repository-standard
runtime/reference-data seeding when established.

### 4. Update transition policy

Keep transition rules centralized. Do not scatter predecessor checks across endpoints and UI components.
Rules should return a structured reason for rejection. Authorization and business validity are separate:
a user may have permission to transition records generally but the specific transition may still be invalid.

### 5. Guard transition writes

Use version/expected-stage conditional writes. The operation must atomically:

1. Verify expected current stage/version.
2. Update current state.
3. Append transition history with from/to, actor, reason, occurred-on UTC, correlation ID.
4. Append audit evidence.
5. Commit once.

Zero affected rows or concurrency-token mismatch maps to 409. Do not create history/audit rows on conflict.

### 6. Update reporting semantics

- Funnel order comes from explicit display order, not enum ordinal.
- Disabled stages remain visible for historical windows when they contain data.
- A new stage can alter conversion denominators; document this.
- Rename labels without changing stable grouping unless business meaning changed.
- Cohort and transition metrics must use transition timestamps, not current-state timestamps.

### 7. Update frontend tokens and labels

- Add/update central CSS token and stage metadata registry.
- Pair color with label/icon.
- Ensure unknown/legacy stage fallback renders safely.
- Update stepper ordering and filters from API/reference data when possible rather than hardcoding twice.
- Verify contrast and screen-reader labels.

### 8. Test

Backend integration tests:

- Allowed transition succeeds.
- Disallowed transition returns correct Problem Details.
- Permission failure.
- Disabled destination rejected.
- Concurrent requests: one succeeds, one conflicts.
- Exactly one history and audit row on success.
- No evidence rows on failure.
- Historical rows resolve renamed/disabled stages.
- Funnel ordering and date-window reporting.

Frontend tests:

- Stage label/icon/token rendering.
- Unknown stage fallback.
- Allowed actions shown by permissions.
- 409 conflict refreshes current state and informs the user.
- Keyboard use and color-independent meaning.

## Dangerous operations

Never do any of these without an explicit data-migration design:

- Delete a stage row used by history.
- Reuse an old code for a new meaning.
- Change an enum numeric value already persisted.
- Rewrite old transition rows to the new stage.
- Infer history from the current-stage column.
- Make reports depend on translated display text.

## Completion checklist

- [ ] Change type is classified.
- [ ] Stable identity and historical meaning are preserved.
- [ ] Transition rules are centralized.
- [ ] Write uses concurrency protection and one transaction.
- [ ] History and audit are append-only and atomic.
- [ ] Reports/funnel semantics are reviewed.
- [ ] Central UI tokens and accessible labels are updated.
- [ ] Backend and frontend edge tests pass.
- [ ] Any migration is deferred to its own microstep.
