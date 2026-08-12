# Concurrency Control

Project Chicago rejects silent last-write-wins for mutable CRM records.

## Pattern
Use an explicit optimistic concurrency token/version in persistence and expose the necessary expected version through update contracts.

## Behavior
1. caller reads record/version,
2. caller submits mutation with expected version,
3. SQL update succeeds only when current version matches,
4. stale update produces a typed conflict,
5. API returns 409,
6. UI tells the user data changed and requires refresh/retry.

## Audit
Only a successful mutation writes the corresponding committed audit/outbox event. A failed stale update must not claim a state change.

## Testing
Two-context SQL integration test should prove the second stale writer fails.
