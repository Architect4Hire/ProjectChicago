# ADR-0005 — Thin Hosts with Layered Service Core

- **Status:** Accepted
- **Requirements:** architecture constraints, TEST-001..007

## Context
HTTP and Function triggers are transport boundaries. Business logic tied to ASP.NET Core or Functions SDK types becomes harder to test and easy to duplicate.

## Decision
Every bounded service uses three sibling projects: HTTP host, `.Core`, and `.Functions`. Both entry points delegate into `.Core`.

Within Core:

```text
Facade → Business → Data → Repository → DbContext
```

Controllers and Functions call Facades only. Data owns transactions. Repositories own persistence/query mechanics. Business owns business rules and translation.

## Consequences
- One service can have multiple entry-point technologies without duplicating rules.
- Layer direction is reviewable and testable.
- The `.Core` project must not become coupled to HTTP/Function SDK concerns.
- Architecture tests should enforce project and namespace boundaries.

## Alternatives considered
- Business logic in controllers/functions: rejected.
- Cross-service Core references: rejected.
- Five assemblies per onion ring: unnecessary for the chosen lightweight structure; the dependency arrow is the invariant.

## Validation
Architecture tests inspect references/usings and the release gate verifies no layer skips inward.
