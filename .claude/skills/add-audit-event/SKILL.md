---
name: add-audit-event
description: >
  Add immutable, security-conscious audit coverage to one CRM business operation, including event naming,
  actor and target identity, safe change details, correlation, transaction placement, read authorization,
  retention considerations, and focused tests.
---
# Add audit coverage

Audit is authoritative evidence of who did what, to which record, when, and through which operation. It is
not application logging and not the customer activity timeline.

## Discovery gate

Before changing code, discover the actual solution/project paths, namespaces, target frameworks, package versions, AppHost resource names, SQLDB connection name, DbContext, migrations assembly, test conventions, and feature location. Never treat example names as repository facts. Stop without editing when a required value cannot be proven. Aspire is required and is the supported source of local SQLDB connection information.

## When audit is required

Audit at minimum:

- Authentication, role, permission, ownership, and assignment changes.
- Account/contact create, merge, archive, restore, and delete.
- Lifecycle transitions and transition-rule changes.
- Opportunity amount/probability/stage changes.
- Export, bulk update, and sensitive-data access where policy requires it.
- Administration/reference-data changes.
- Failed privileged operations when the security policy requires attempts to be recorded.

Routine reads generally belong in telemetry, not the immutable audit table, unless compliance requires read
auditing.

## Audit event contract

Define a stable action name such as `Contact.EmailChanged` or `Lifecycle.StageTransitioned`.
Capture:

- Event ID.
- Occurred-on UTC.
- Actor user ID and actor type.
- Effective/impersonated actor if supported.
- Action name and module.
- Target type and target ID.
- Parent/account ID when useful for authorized retrieval.
- Correlation/trace ID.
- Source/channel (`Api`, `Import`, `Automation`, `Admin`).
- Outcome when auditing attempts.
- Safe structured details or field-level changes.

Do not store access tokens, passwords, full authorization headers, connection strings, unrestricted request
bodies, sensitive notes, or binary data. Store old/new values only for approved fields. For sensitive
fields, record `changed: true` or a redacted/hash representation according to policy.

## Procedure

1. Name the business fact and reason it needs audit coverage.
2. Locate the operation's transaction boundary.
3. Reuse the central audit writer; do not insert audit entities directly from every module.
4. Build actor/correlation/source from trusted server context, not client input.
5. Build safe details explicitly; do not serialize the command object wholesale.
6. Write success evidence in the same transaction as the state change.
7. For failed-attempt auditing, use the repository-approved separate mechanism and clearly distinguish
   `Attempted` from `Succeeded`; never create a false success event.
8. Add read authorization and filtering if a new audit query is introduced.
9. Add tests.

## Transaction rule

For successful business changes, state and audit are one unit:

```text
begin transaction
  update authoritative state
  append lifecycle/timeline history if applicable
  append audit record
commit
```

If any leg fails, none commit. Do not publish an audit entry after the transaction as a fire-and-forget task.

## Reading audit data

- Audit records are append-only; no general update/delete endpoint.
- Require privileged policy.
- Apply record/organization scope.
- Paginate and order by occurred-on + ID for stable ties.
- Return explicit response contracts, not raw detail JSON when that could expose newly added fields.
- Consider retention/legal-hold rules before adding cleanup behavior.

## Tests

- Success writes exactly one event with correct action, actor, target, UTC timestamp, and correlation ID.
- State change and audit roll back together on failure.
- Validation/business rejection does not produce success evidence.
- Unauthorized caller cannot read audit data.
- Sensitive values are absent/redacted.
- Repeated/idempotent request does not create duplicate success facts unless each attempt is intentionally
  auditable.

## Completion checklist

- [ ] Action name is stable and past-tense/factual.
- [ ] Actor and correlation come from trusted context.
- [ ] Details are allow-listed and safe.
- [ ] Success audit is atomic with state.
- [ ] Timeline and telemetry responsibilities remain separate.
- [ ] Read access is privileged, scoped, ordered, and paginated.
- [ ] Rollback, unauthorized, redaction, and duplicate behavior are tested.
