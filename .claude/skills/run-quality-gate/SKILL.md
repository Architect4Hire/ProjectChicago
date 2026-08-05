---
name: run-quality-gate
description: >
  Verify the Lifecycle CRM repository without changing code. Runs the appropriate restore, formatting,
  architecture, backend, frontend, integration, OpenAPI, migration-cleanliness, and optional Playwright
  checks; records exact failures and distinguishes product defects from environment failures.
---
# Run the quality gate

This is a read/execute-only skill. Do not fix failures in the same invocation.

## Discovery gate

Before changing code, discover the actual solution/project paths, namespaces, target frameworks, package versions, AppHost resource names, SQLDB connection name, DbContext, migrations assembly, test conventions, and feature location. Never treat example names as repository facts. Stop without editing when a required value cannot be proven. Aspire is required and is the supported source of local SQLDB connection information.

## Inspect scripts first

Read solution files, `Directory.Build.props`, package scripts, CI workflow, and test project layout. Use the
same commands CI uses. Do not invent a parallel local gate.

## Gate levels

### Focused gate

For one module/feature:

- Format/lint touched files if the repo supports scoped checks.
- Build affected project.
- Run tests filtered to module/feature.

### Backend gate

Typical shape:

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes
dotnet build --no-restore
# unit + architecture + integration according to solution/CI
dotnet test --no-build
```

Also verify when applicable:

- `dotnet ef migrations has-pending-model-changes`.
- OpenAPI document generation.
- Architecture tests for module boundaries.

### Frontend gate

Typical shape:

```bash
npm ci
npm run lint
npm test -- --watch=false
npm run build
```

Run Playwright only when CI normally does or the user scoped an end-to-end gate. Confirm required API/test
dependencies are available first.

### Full gate

Run the repository CI-equivalent sequence, including disposable SQL Server/integration dependencies and
Playwright when configured.

## Failure classification

For each failure report:

- Exact command and exit code.
- First actionable error, not only final summary.
- Affected project/test.
- Product failure, flaky test, missing dependency, configuration, or environment/tooling failure.
- Whether later checks were skipped because prerequisites failed.
- Whether the failure predates current changes, only when proven from a clean baseline/CI evidence.

Do not label a failure “pre-existing” by assumption.

## Guardrails

- Do not mutate source, snapshots, migrations, lock files, generated clients, or goldens.
- Do not run database migrations against non-disposable/shared environments.
- Do not use `--no-verify`, skip tests, or relax thresholds.
- Do not rerun repeatedly until a flaky test passes without reporting the flake.
- Redact secrets from command output.

## Report format

```text
Gate: focused | backend | frontend | full
Environment: ...
PASS  command
FAIL  command
  first error: ...
SKIP  command
  reason: prerequisite failed
Result: pass/fail
Required next microstep: ...
```

## Completion checklist

- [ ] CI-equivalent commands were discovered and used.
- [ ] No source/generated artifact was changed.
- [ ] Exact commands and outcomes are reported.
- [ ] Failures are classified with evidence.
- [ ] Skipped checks are explicit.
- [ ] Fixes are deferred to separate microsteps.
