# Project Chicago SCRUB Prompts

The canonical implementation sequence is:

- [Project Chicago SCRUB Micro-Prompts](project-chicago-scrub-microprompts.md)

A focused audit extraction/review runbook is also provided:

- [Audit SCRUB Prompts](audit-scrub-prompts.md)

## Execution rules

Run **one prompt at a time**. Each prompt performs one primary action, verifies it and stops.

Before implementation:

- read `CLAUDE.md`,
- read referenced requirement IDs,
- read applicable `.claude/rules`,
- use applicable `.claude/skills`,
- inspect existing code,
- stop at unresolved architecture/security/public-contract/data-ownership decisions.

Do not skip the architecture gates at the start of the sequence.

## SCRUB

- **Scope** — exactly one action.
- **Constraints** — architecture/stack/invariants/tests.
- **Restrictions** — adjacent work that must not begin.
- **Usage** — rules/skills/tools/context.
- **Behavior** — inspect → change → verify → report → stop.

The prompt library is source-controlled engineering material. When architecture or requirements change, update prompts so future generated code follows the new truth.
