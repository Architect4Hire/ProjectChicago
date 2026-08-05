---
name: plan-microstep
description: >
  Reduce a CRM change into one reviewable Claude Code action with one observable outcome, exact scope,
  exclusions, files, invariants, dependencies, verification, and stop conditions. Use before implementation
  when a request spans entity, mapping, migration, endpoint, generated client, or UI work.
---
# Plan a microstep

A microstep changes one kind of artifact or proves one fact. It should be reviewable as one focused commit
and leave the repository in a buildable or intentionally documented intermediate state.

## Discovery gate

Before changing code, discover the actual solution/project paths, namespaces, target frameworks, package versions, AppHost resource names, SQLDB connection name, DbContext, migrations assembly, test conventions, and feature location. Never treat example names as repository facts. Stop without editing when a required value cannot be proven. Aspire is required and is the supported source of local SQLDB connection information.

## Atomicity test

A proposed step is too large when its title contains “and” joining independent deliverables, or when it
combines two of these:

- Domain entity/value object.
- EF configuration.
- DbContext registration.
- Migration generation.
- Migration application.
- Request/response contract.
- Business operation.
- HTTP endpoint.
- OpenAPI client regeneration.
- Angular facade/state.
- Angular component.
- Playwright workflow.

Tests for the one artifact may be included when they are the direct proof of that artifact. A broad quality
gate is its own step.

## Procedure

1. Read root `CLAUDE.md` and applicable rules/skills.
2. Inspect one comparable implementation.
3. State the single observable outcome in one sentence.
4. Name the owning module/feature.
5. List exact files expected to change and files explicitly not to change.
6. State contract, database, lifecycle, audit, authorization, reporting, frontend, and migration impact.
7. State preconditions and dependencies on earlier microsteps.
8. State invariants that must remain true.
9. Choose the smallest verification command/test.
10. Define stop conditions.
11. Stop before editing unless implementation was explicitly requested.

## Required plan format

```text
Microstep: <verb + one artifact/outcome>
Owner: <module/feature>
Outcome: <one observable result>
Preconditions: <what must already exist>
In scope:
- ...
Out of scope:
- ...
Files:
- modify/create ...
Invariants:
- ...
Verification:
- exact command/test
Stop when:
- ...
Follow-up microsteps:
1. ...
```

## Example split

Bad: “Add Contact entity, configure EF, migrate database, expose CRUD API, and build Angular page.”

Good sequence:

1. Add Contact domain entity.
2. Add Contact EF configuration.
3. Register Contact set/configuration in `<db-context>`.
4. Generate `AddContacts` migration.
5. Apply migration to disposable development database.
6. Add create-contact request/response contracts.
7. Add create-contact Business operation.
8. Map create-contact endpoint.
9. Add create-contact HTTP integration test.
10. Regenerate Angular API client.
11. Add contact data-access facade.
12. Add contact form component.
13. Integrate form into account page.
14. Add Playwright create-contact journey.

## Stop conditions

- Ownership cannot be determined.
- Comparable pattern conflicts with documentation.
- Existing build/migration/client generation is broken.
- Requested step would require a destructive contract/data decision.
- Required policy or business rule is unspecified.

## Completion checklist

- [ ] One verb and one observable outcome.
- [ ] Exact owner, files, and exclusions.
- [ ] All cross-cutting impacts considered.
- [ ] One smallest verification.
- [ ] Clear stop condition.
- [ ] Follow-up sequence preserves buildability and dependency order.
