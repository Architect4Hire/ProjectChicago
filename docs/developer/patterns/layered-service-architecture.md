# Layered Service Architecture

## Intent
Keep entry-point technology outside business behavior while making transaction/persistence boundaries explicit.

```text
Controller / Function → Facade → Business → Data → Repository → DbContext
```

## Responsibilities
- **Controller/Function:** bind transport, trusted context, delegate, map response/failure.
- **Facade:** use-case validation, authorization/scope orchestration, cache check only if ever approved.
- **Business:** business rules, state transitions, model translation, audit facts.
- **Data:** transaction boundary and coordination of persistence operations.
- **Repository:** EF/SQL query/persistence mechanics.
- **DbContext:** service-owned unit of work/schema.

## Forbidden shortcuts
- Controller → Repository.
- Function → DbContext.
- Business → DbContext.
- one service → another service's Core.
- service-local model in `Contracts`.

## Why this shape
The Core library remains testable from HTTP and Function entry points without duplicating rules. The architecture is the dependency arrow; folder names help humans enforce it.

## Verification
Architecture tests plus code review. If a layer needs data from a deeper layer, extend the next interface rather than skipping a ring.
