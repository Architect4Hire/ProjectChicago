# Frontend Gateway Integration

React calls one configured gateway origin/base URL.

## Shared client
Centralize:
- base URL,
- approved credential/session behavior,
- ProblemDetails parsing,
- cancellation,
- trace/support reference extraction.

Feature modules do not construct internal service URLs.

## Contracts
TypeScript models mirror public API contracts, not EF/domain classes.

## UX failure states
Every data feature deliberately handles loading, empty, validation, unauthorized/forbidden, concurrency conflict and unexpected failure.

## Authorization
UI may hide controls based on current user role, but backend authorization remains authoritative.
