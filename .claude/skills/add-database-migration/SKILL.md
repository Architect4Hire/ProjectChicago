---
name: add-database-migration
description: >
  Generate and review exactly one EF Core migration for the Lifecycle CRM SQL Server/Azure SQL Database (SQLDB) after model
  changes are complete. Verifies model cleanliness, names the owning module, reviews destructive and locking
  risk, validates upgrade/rollback shape, and stops before applying.
---
# Add a database migration

Migration generation and migration application are separate microsteps. This skill generates and reviews
one migration; it does not update any database.

## Discovery gate

Before changing code, discover the actual solution/project paths, namespaces, target frameworks, package versions, AppHost resource names, SQLDB connection name, DbContext, migrations assembly, test conventions, and feature location. Never treat example names as repository facts. Stop without editing when a required value cannot be proven. Aspire is required and is the supported source of local SQLDB connection information.

## Preconditions

- Entity and EF configuration changes are complete and reviewed.
- The solution builds.
- The current snapshot has no unexplained pending changes.
- The migration name and owning module are known.
- No unrelated model changes are present in the working tree.

## Procedure

### 1. Inspect state

Run repository-equivalent commands:

```bash
git status --short
dotnet build
dotnet ef migrations list --project <data-project> --startup-project <api-project> --context <db-context>
dotnet ef migrations has-pending-model-changes --project <data-project> --startup-project <api-project> --context <db-context>
```

The final command may be expected to report pending changes before generation, but inspect that they match
the intended model diff only.

### 2. Choose a precise name

Use business/schema intent, e.g.:

- `AddContactCommunicationPreferences`
- `AddLifecycleTransitionReason`
- `IndexOpportunityOwnerAndStage`

Avoid `UpdateDatabase`, `Changes`, dates, or ticket-only names.

### 3. Generate exactly one migration

Use the canonical paths from root `CLAUDE.md`. Example:

```bash
dotnet ef migrations add <MigrationName> \
  --project <api-project-path> \
  --startup-project <api-project-path> \
  --context <db-context> \
  --output-dir Infrastructure/Persistence/Migrations
```

Do not guess paths; adapt to the actual repository.

### 4. Review generated code and snapshot

Report:

- Tables/columns added, altered, renamed, or dropped.
- Nullability/default changes.
- Primary/foreign keys and delete behavior.
- Unique/check constraints.
- Indexes, ordering, filters, and included columns.
- Seed/reference-data changes.
- Data movement/backfill SQL.
- Potential table rewrites, long locks, or full scans.
- `Down` behavior and whether rollback loses data.
- Whether generated T-SQL is correct, including `rowversion`, filtered indexes, Unicode/length choices, default constraints, computed columns, identity behavior, cascade rules, and online/index considerations where relevant.

Treat drop/rename detection carefully: EF may generate drop+add where a rename was intended. Do not hand-edit
in this same microstep; report that a dedicated migration-repair microstep is required.

### 5. Validate artifacts

- Rebuild.
- Confirm the snapshot compiles.
- Generate an idempotent T-SQL script only when the repository workflow requires review or deployment packaging for inspection if the repo supports it.
- Re-run `has-pending-model-changes`; it must be clean.

Do not run `database update`.

## Destructive-risk checklist

Stop and request explicit review when the migration:

- Drops or narrows a populated column.
- Makes a nullable populated column non-null without a safe backfill.
- Rebuilds a large table/index.
- Changes cascade-delete behavior.
- Changes stable lifecycle/reference IDs.
- Rewrites timestamps/timezones.
- Introduces a unique constraint over potentially dirty data.
- Uses raw SQL that is not safely reversible.

## Testing expectations

A later application microstep should apply the migration to a disposable SQL Server/Azure SQL Database (SQLDB) and run
integration tests. Production rollout may require expand/migrate/contract sequencing; record this in the
migration notes.

## Completion checklist

- [ ] Exactly one intended model diff is present.
- [ ] One precisely named migration is generated.
- [ ] Migration and snapshot are reviewed line by line.
- [ ] Destructive/locking/backfill risks are reported.
- [ ] Build passes and pending model changes are clean.
- [ ] Database update was not run.
