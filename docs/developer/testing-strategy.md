# Project Chicago Testing Strategy

## Principle

Test at the boundary where a class of defect actually occurs. A mocked repository cannot prove SQL translation; a controller test cannot prove Service Bus idempotency.

## Test layers

### Unit tests
Best for:
- Client/Project/Task state rules,
- mapping/normalization,
- validation,
- authorization policy logic where pure,
- envelope serialization,
- correlation primitives.

### SQL integration tests
Run against SQL Server-compatible infrastructure for:
- EF mappings,
- foreign keys/indexes,
- LINQ translation,
- pagination/filter/sort,
- optimistic concurrency,
- transactional rollback,
- state + outbox atomicity,
- inbox idempotency.

Do not use EF Core InMemory as proof of relational behavior.

### API integration tests
Prove:
- HTTP status and public contract,
- authentication/authorization,
- validation,
- 404/409 behavior,
- routing/OpenAPI,
- safe ProblemDetails.

### Function tests
Prove trigger adapters:
- binding/deserialization,
- delegation,
- cancellation,
- trace/correlation propagation,
- failure propagation.

Core integration tests prove consumer idempotency.

### React tests
Prove:
- loading/empty/error/unauthorized states,
- form validation and server errors,
- filters/pagination,
- concurrency conflict handling,
- keyboard/accessibility behavior,
- role-based affordances.

### Architecture tests
Enforce:
- no cross-service Core refs,
- no cross-service DbContext access,
- Controller/Function → Facade only,
- layer direction,
- no HTTP-trigger Functions,
- no request-path BackgroundService outbox processor,
- Gateway persistence/broker-free,
- React contains no internal service URLs,
- no PostgreSQL dependencies.

### End-to-end proof
At least one Client creation must prove:
YARP → CRM → SQL/outbox → timer Function → Service Bus → Audit Function → AuditDb with one logical correlation and idempotent replay.

## Release gate

Run:
1. all .NET builds/tests,
2. SQL integration suites,
3. messaging reliability matrix,
4. route security matrix,
5. React lint/tests/build,
6. accessibility checks,
7. architecture review,
8. end-to-end trace proof.
