# Project Chicago — Service Implementation Template

**Purpose:** Reusable checklist for scaffolding each bounded service (CRM, Identity, Audit, Notification, Search, Workflow)

---

## Scaffolding Checklist

### Project Structure
- [ ] `ProjectChicago.<Service>` — ASP.NET Core HTTP host (API controllers, middleware, composition)
- [ ] `ProjectChicago.<Service>.Core` — Domain, application logic, business rules, repositories
- [ ] `ProjectChicago.<Service>.Functions` — Azure Functions entry points (Service Bus triggers, timer triggers)

### Database
- [ ] SQL Server database created: `projectchicago_<service_lowercase>`
- [ ] Service owns exactly one database
- [ ] Entity Framework DbContext created (`<Service>DbContext`)
- [ ] Initial migration generated
- [ ] EF model includes: entities, configurations, seed data if applicable

### Dependencies and NuGet
- [ ] `ProjectChicago.Shared` referenced (outbox/inbox, correlation, error contracts)
- [ ] `ProjectChicago.Contracts` referenced if publishing integration events
- [ ] OpenTelemetry packages included
- [ ] Service Bus SDK included (if consuming or publishing events)
- [ ] EF Core SQL Server provider included

### Aspire Configuration
- [ ] Service registered in `ProjectChicago.AppHost`
- [ ] Database resource added to AppHost
- [ ] Health check endpoint configured
- [ ] Service Bus resource referenced (if messaging)
- [ ] OpenTelemetry exporter configured
- [ ] ServiceDefaults/common configuration applied

### Onion Layering (Controller → Facade → Business → Data → Repository)

#### HTTP Controller / Function Trigger
- [ ] Route(s) defined with operation IDs
- [ ] Authentication/authorization attributes applied
- [ ] Request validation performed before facade call
- [ ] Correlation context extracted and passed
- [ ] Response mapped to consistent HTTP contracts (200/201/400/403/404/409/500)
- [ ] Error details use ProblemDetails format
- [ ] Trace/support reference included in error responses

#### Facade Layer
- [ ] Input validation (nulls, ranges, format)
- [ ] Authorization policy checks applied
- [ ] Use-case boundary defined
- [ ] Orchestration logic (multiple repository calls, cache checks)
- [ ] Delegation to Business layer
- [ ] Exception mapping (validation → 400, auth → 403, not-found → 404, concurrency → 409)

#### Business Layer
- [ ] Domain decisions (state machines, invariants, rules)
- [ ] Model translation (DTOs ↔ entities)
- [ ] Outbox fact generation (list of domain events)
- [ ] No direct repository calls, no EF queries
- [ ] No transactional boundaries (Data layer owns those)
- [ ] No Service Bus calls (async publishing happens through outbox)

#### Data Layer
- [ ] Repository orchestration
- [ ] Transaction boundary definition (`DbContext.SaveChangesAsync()`)
- [ ] Outbox record insertion (alongside domain mutation)
- [ ] Inbox record update (idempotency for consumed messages)
- [ ] Optimistic concurrency check (if applicable)
- [ ] Error translation (SQL constraint violations → domain exceptions)

#### Repository
- [ ] Persistence operations for one entity
- [ ] No business logic
- [ ] No joins to other services' entities
- [ ] Parameterized queries (no SQL injection)
- [ ] Efficient queries (avoid N+1)

### Integration Events

#### Publishing (Outbox Pattern)
- [ ] Event DTOs created in `ProjectChicago.Contracts` (if cross-service)
- [ ] Outbox record structure: EventId, EventType, Version, Payload, PublishedUtc
- [ ] Business layer emits `DomainEvent` facts during transaction
- [ ] Data layer persists domain state + outbox record atomically
- [ ] Integration tests verify atomicity (publish failure = domain change rolled back)

#### Timer-Triggered Outbox Relay
- [ ] Function created in `.Functions` project
- [ ] Lease logic: `SELECT TOP(N) ... WHERE PublishedUtc IS NULL`
- [ ] Publish to Service Bus with envelope: EventId, EventType, Version, OccurredUtc, CorrelationId, CausationId, TraceId, ActorId, SourceService, Payload
- [ ] Mark published: `UPDATE Outbox SET PublishedUtc = ... WHERE EventId IN (...)`
- [ ] Idempotency: Publish with IdempotencyKey or retry-safe logic
- [ ] Tests cover: normal flow, duplicate publish, Service Bus failure, poison message

#### Consuming (Service Bus Trigger)
- [ ] Function trigger defined: `[ServiceBusTrigger("topic-name", "subscription-name")]`
- [ ] Inbox idempotency: check `Inbox` table for MessageId before processing
- [ ] Envelope deserialization with version handling
- [ ] Trace context linked: extract TraceId, CorrelationId, CausationId from message properties
- [ ] Payload deserialization (handle schema versioning)
- [ ] Delegate to service `.Core` business logic
- [ ] Insert inbox record after processing
- [ ] Transactional: all-or-nothing (fail the Function if business operation fails)
- [ ] Tests cover: normal flow, duplicate delivery (idempotency), invalid message, transient failure (retry), permanent failure (dead-letter)

### Observability

#### OpenTelemetry Setup
- [ ] `ActivitySource` created per service
- [ ] Activities created for business operations (e.g., `Client.Create`, `Task.Assign`)
- [ ] HTTP request/response instrumentation enabled
- [ ] SQL query instrumentation enabled
- [ ] Service Bus operation instrumentation enabled (if applicable)
- [ ] Resource attributes: service name, version, deployment environment
- [ ] Custom attributes: ClientId, ProjectId, TaskId, UserId, EntityId (where safe)

#### Structured Logging
- [ ] Logger configured for the service
- [ ] Structured properties include: CorrelationId, TraceId, UserId (where applicable)
- [ ] Sensitive data redacted (no passwords, tokens, full payloads)
- [ ] Log levels used consistently: Debug (development), Info (key operations), Warning (recoverable issues), Error (unrecoverable)
- [ ] Correlation ID automatically attached to logs via instrumentation

#### Correlation Context
- [ ] CorrelationId extracted from request/message headers (or generated)
- [ ] CorrelationId propagated to downstream calls (HTTP, SQL, Service Bus)
- [ ] TraceId linked from W3C trace context
- [ ] CausationId set for message-triggered operations

### Security

#### Authentication & Authorization
- [ ] Controllers/Functions require `[Authorize]` where applicable
- [ ] Policy-based authorization implemented
- [ ] Resource-level authorization (e.g., "Can user read this Client?")
- [ ] Roles (Administrator, Manager, Contributor, ReadOnly) enforced
- [ ] `ICurrentUser` abstraction used to resolve actor context
- [ ] Actor ID never accepted from client requests

#### Input Validation
- [ ] All inputs validated before business logic
- [ ] Emails, phones, URLs normalized
- [ ] Identifiers validated for format/range
- [ ] Paging/sorting/filtering parameters bounded
- [ ] Validation errors return 400 with property-to-messages mapping

#### Sensitive Data
- [ ] Passwords/tokens/credentials never logged
- [ ] PII minimized in logs (e.g., store hashed email for audit, not plaintext)
- [ ] Audit events redact sensitive fields
- [ ] Database backups encrypted

### Testing

#### Unit Tests
- [ ] Facade: input validation, authorization, orchestration
- [ ] Business: domain rules, state machines, model translation
- [ ] Data: transaction boundaries, optimistic concurrency, error translation
- [ ] Repository: query correctness, efficiency, parameterization

#### Integration Tests
- [ ] API endpoints (happy path, validation failure, auth failure, not-found, concurrency conflict, 5xx)
- [ ] Outbox atomicity (domain change + event record in same transaction)
- [ ] Outbox relay (publish, idempotency, retry, poison message)
- [ ] Event consumption (normal flow, duplicate delivery, schema versioning, invalid message)
- [ ] Database persistence (correct schema, constraints, migrations)
- [ ] Correlation context flows through all layers

#### Contract/Performance Tests
- [ ] OpenAPI contract validation (generated client matches API)
- [ ] Response time p95 < 500ms for interactive requests
- [ ] Pagination tested (unbounded results blocked, default page size, sort stability)
- [ ] No N+1 queries

### Deployment

#### Aspire Local
- [ ] Service runs in local Aspire orchestration
- [ ] Database migrations applied automatically on startup
- [ ] Service Bus resources available (emulator on Windows)
- [ ] Health check endpoint responds

#### Configuration
- [ ] Connection strings externalized (Aspire, environment variables, Azure Key Vault)
- [ ] No hardcoded endpoints, secrets, credentials
- [ ] Feature flags/toggles (if applicable)
- [ ] Logging level configurable per environment

#### Azure (Flex Consumption)
- [ ] Function App created for `<Service>.Functions`
- [ ] App Service created for `<Service>` HTTP host (or Container Instance)
- [ ] SQL Database created: `projectchicago_<service>`
- [ ] Managed Identity configured for least-privilege resource access
- [ ] Service Bus topic subscriptions configured
- [ ] Application Insights linked
- [ ] Health check alerts configured

### Documentation

#### README.md (per service)
- [ ] Service purpose and responsibilities
- [ ] Domain entities and key operations
- [ ] Integration event list (published/consumed)
- [ ] Development setup (local Aspire)
- [ ] Key design decisions

#### OpenAPI / Swagger
- [ ] All endpoints documented
- [ ] Operation IDs assigned (stable, unique)
- [ ] Request/response models defined
- [ ] Error responses documented
- [ ] Authentication/authorization requirements noted

---

## Service-Specific Checklists

### CRM Service Additions
- [ ] Client, Project, Task entities with lifecycle states
- [ ] Archival logic (soft-delete)
- [ ] Concurrency tokens (optimistic locking)
- [ ] Search/filter endpoints with pagination
- [ ] Dashboard summary endpoint
- [ ] Outbox for 8+ domain events
- [ ] Tests for lifecycle transitions, archival, concurrency

### Identity Service Additions
- [ ] ASP.NET Core Identity DbContext and tables
- [ ] User create/update/activate/deactivate endpoints
- [ ] Role/claims management
- [ ] Password reset workflow
- [ ] Account lockout policy
- [ ] Outbox for 6+ auth/account events
- [ ] Tests for account lifecycle, lockout, password reset

### Audit Service Additions
- [ ] Audit entity (append-only, no PK/FK updates)
- [ ] Inbox table (MessageId, Processed flag)
- [ ] Service Bus trigger consumes all published events
- [ ] Event-to-audit-record mapping (redaction, value extraction)
- [ ] Read-only query API (audit history, activity timeline)
- [ ] Tests for: append-only guarantee, duplicate message handling, redaction

### Notification Service Additions
- [ ] Notification rule definition entity
- [ ] User preference entity
- [ ] Notification history entity
- [ ] Rule engine (condition evaluation, action execution)
- [ ] Channel abstraction (in-app, email, webhook)
- [ ] Outbox for 3+ notification events
- [ ] Tests for: rule evaluation, multi-channel delivery, preference respect

### Search Service Additions
- [ ] Denormalized Client/Project/Task schema
- [ ] Full-text search support (SQL FTS or index)
- [ ] Search result pagination, filtering, sorting
- [ ] Index update logic (from consumed CRM events)
- [ ] Eventual consistency tests (event → index lag)
- [ ] Tests for: index synchronization, search accuracy, archival exclusion

### Workflow Service Additions
- [ ] Workflow rule definition entity (JSON conditions/actions)
- [ ] Workflow execution history entity
- [ ] Compensation log entity
- [ ] Rule engine (evaluate conditions, execute actions)
- [ ] Action library (CreateTask, UpdateProject, PublishEvent, etc.)
- [ ] Outbox for 4+ workflow events
- [ ] Tests for: rule evaluation, action execution, compensation/rollback

---

## Definition of Service Ready

A service is ready for integration testing when:

1. ✓ All three projects (HTTP, .Core, .Functions) compile and run
2. ✓ Database schema created and migrations working
3. ✓ Onion layering implemented (Controller → Facade → Business → Data → Repository)
4. ✓ One happy-path endpoint tested (request → database → response)
5. ✓ Outbox mechanism working (domain change + event record persisted atomically)
6. ✓ Timer Function able to drain outbox to Service Bus
7. ✓ If consuming events: Service Bus trigger able to consume and process
8. ✓ Correlation context flowing through (TraceId, CorrelationId in logs and telemetry)
9. ✓ Error handling returns consistent ProblemDetails format
10. ✓ Tests passing (unit, integration, contract)

---

## References

- [ADR-0015: Bounded-Context Catalog](adr-0015-bounded-context-catalog.md)
- [Integration Event Catalog](integration-event-catalog.md)
- [CLAUDE.md](../CLAUDE.md) — Architecture rules
- [Service Implementations](../CLAUDE.md#usage) — Skill and pattern reference
