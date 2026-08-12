# Project Chicago — Architecture Summary

**Status:** ADR-0015 Approved  
**Date:** 2026-08-12  
**Scope:** Six-service initial bounded-context catalog with full integration topology

---

## Executive Summary

Project Chicago shall launch with six independently deployable bounded services, each owning one SQL database and communicating through HTTP APIs and Service Bus integration events:

1. **CRM Service** — Clients, Projects, Tasks lifecycle
2. **Identity Service** — ASP.NET Core Identity and user management
3. **Audit Service** — Append-only audit trail and compliance logging
4. **Notification Service** — Event-driven notifications (in-app, email, webhooks)
5. **Search Service** — Denormalized read-model for full-text search and filtering
6. **Workflow Service** — Automation rules and orchestration engine

---

## Architecture Documents

### Decisions
- **[ADR-0015: Bounded-Context Catalog](docs/design/adr-0015-bounded-context-catalog.md)** — Complete service architecture, responsibilities, database ownership, deployment topology, integration patterns, and rationale for each service

### Design
- **[High-Level Design](docs/design/high-level-design.md)** — Updated system context, container view (all six services), architectural drivers, and design gates
- **[Integration Event Catalog](docs/design/integration-event-catalog.md)** — All 35+ events published by each service, event envelope standard, schema definitions, sensitive-value redaction rules, subscription matrix, versioning strategy
- **[Ongoing Architecture Plan](docs/design/ongoing-architecture-plan.md)** — Phased implementation roadmap with acceptance criteria for each service and integration layer

### Requirements
- **[Extended Service Requirements](docs/requirements/lightweight-crm-product-and-system-requirements.md#43a-notification-service-requirements)** — NOTIF (Notification), SEARCH (Search), WORKFLOW (Workflow) requirements (sections 43a–43c)
- **[Requirements Traceability](docs/design/requirements-traceability.md)** — Updated to map NOTIF, SEARCH, WORKFLOW to prompts and ADRs

---

## Service Responsibilities at a Glance

| Service | Owns | Publishes | Consumes | Functions |
|---------|------|-----------|----------|-----------|
| **CRM** | Client, Project, Task entities | 8 entity events | (none) | Outbox relay (timer) |
| **Identity** | Users, roles, credentials | 6 auth/account events | (none) | Outbox relay (timer) |
| **Audit** | Append-only audit log | (none) | All 35+ events | Event ingestion (Service Bus trigger) |
| **Notification** | Rules, templates, history | 3 delivery events | 20+ CRM/Identity events | Rule evaluation + delivery (triggers) |
| **Search** | Denormalized CRM index | 1 index-update event | 12 CRM entity events | Index synchronization (triggers) |
| **Workflow** | Rule templates, execution history | 4 automation events | 15+ CRM/Identity events | Rule evaluation + action execution (triggers) |

---

## Key Integration Patterns

### Synchronous Communication
- **HTTP APIs only** through YARP gateway
- CRM ↔ Identity, CRM ↔ Audit, Notification ↔ CRM (for rule context)
- No cross-database queries; strong service boundaries

### Asynchronous Communication
- **Transactional Outbox + Service Bus**
- All services publish through outbox → timer-triggered relay → Service Bus topic
- Audit subscribes to all events for append-only trail
- Notification, Search, Workflow subscribe to domain events for evaluation

### Event Flow Example
```
1. User creates Task via React
2. Gateway routes to CRM API
3. CRM Controller → Facade → Business → Data
4. Data layer: INSERT Task + INSERT OutboxMessage (atomic transaction)
5. CRM Timer Function: lease outbox, publish to Service Bus, mark dispatched
6. Service Bus delivers: Crm.Task.Created.v1 event
7. Audit Function: consume, INSERT audit record in AuditDb
8. Notification Function: consume, evaluate rules, send notifications
9. Search Function: consume, update denormalized index
10. Workflow Function: consume, evaluate automation rules
11. All functions link back to original request trace ID
```

---

## Database Ownership

| Service | Database | Owner | Contents |
|---------|----------|-------|----------|
| CRM | `projectchicago_crm` | CRM team | Clients, Projects, Tasks, outbox, inbox |
| Identity | `projectchicago_identity` | Identity team | Users, roles, claims, outbox, inbox |
| Audit | `projectchicago_audit` | Compliance | Append-only audit log, inbox (idempotency), no mutations |
| Notification | `projectchicago_notification` | Notification team | Rules, templates, preferences, history |
| Search | `projectchicago_search` | Search team | Denormalized Client/Project/Task index, index metadata |
| Workflow | `projectchicago_workflow` | Workflow team | Rule definitions, execution history, compensation log |

**Invariant:** One service, one database. No cross-service queries. Cross-service access only through HTTP APIs.

---

## Service Bus Topology

```
Topic: ProjectChicago.Events

Subscriptions:
  - Audit              (all events, no filtering)
  - Notification       (CRM.*, Identity.User.*, Identity.Password.* [filtered])
  - Search             (Client.*, Project.*, Task.* [filtered])
  - Workflow           (Client.*, Project.*, Task.*, Identity.User.* [filtered])
```

Each subscription maintains idempotency through inbox records in the consuming service's database.

---

## Deployment Architecture

**Azure deployment model (Flex Consumption Functions + App Service/Container Instances HTTP hosts):**

```
ProjectChicago.Crm                 → projectchicago-crm-app + projectchicago-crm-func
ProjectChicago.Identity            → projectchicago-identity-app + projectchicago-identity-func
ProjectChicago.Audit               → projectchicago-audit-app + projectchicago-audit-func
ProjectChicago.Notification        → projectchicago-notification-app + projectchicago-notification-func
ProjectChicago.Search              → projectchicago-search-app + projectchicago-search-func
ProjectChicago.Workflow            → projectchicago-workflow-app + projectchicago-workflow-func
```

Each service:
- Independently deployable
- Uses Managed Identity for Azure resource access (least privilege)
- Exports OpenTelemetry to shared Application Insights
- Connects to shared Service Bus (subscription-scoped)

---

## Traceability and Observability

Every request and event carries:
- **TraceId** — Distributed trace correlation
- **CorrelationId** — User/business operation correlation
- **CausationId** — Event causation chain
- **SpanId / ParentSpanId** — W3C trace hierarchy

**End-to-end trace example:**
```
Browser request → Gateway → CRM API → [outbox] → Timer Function 
→ Service Bus → Audit Function → AuditDb
↓ (all steps carry same TraceId)
Azure Monitor / Application Insights (unified view)
```

Operators can:
1. Start with user-reported issue reference / Trace ID
2. Open Application Insights
3. Find gateway request → follow trace → identify API → identify dependency → follow async Function → identify database change → correlate to audit record

---

## Audit and Compliance

**Audit Service (append-only immutable trail):**
- Consumes all 35+ events published by CRM, Identity, Notification, Search, Workflow
- Appends immutable audit records (no UPDATE/DELETE in normal workflows)
- Stores: EventId, Entity, EntityId, Action, Timestamp, ActorId, TraceId, Previous/New values
- Redacts: passwords, tokens, credentials, secrets before storage
- Provides read-only query API (audit history, activity timeline, audit report export)

Satisfies [AUDIT-001..008](docs/requirements/lightweight-crm-product-and-system-requirements.md#audit-001).

---

## Event Versioning and Stability

- Events are versioned external contracts (e.g., `Crm.Client.Created.v1`)
- Breaking changes increment major version
- Consumers must handle unexpected schema versions gracefully
- Payload includes version field for forward/backward compatibility
- Testing must verify version compatibility and redaction rules

---

## Constraints and Invariants

1. ✓ **One database per service** — No shared databases, no cross-database queries
2. ✓ **Transactional outbox** — Domain change + event record committed atomically
3. ✓ **Append-only audit** — No mutations to audit records after append
4. ✓ **Idempotent consumers** — All service Functions tolerate duplicate message delivery
5. ✓ **Correlation flow** — TraceId, CorrelationId, CausationId flow end-to-end
6. ✓ **Sensitive redaction** — Passwords, tokens, credentials never logged/stored in audit/events
7. ✓ **YARP-only gateway** — React never calls service endpoints directly
8. ✓ **Managed Identity** — Azure services use Managed Identity, no connection string hardcoding

---

## Next Steps

### Phase 1 — Shared platform (Decision Gate: ADR-0018, ADR-0016, ADR-0017)
- Solution structure, build, package management
- Aspire AppHost and ServiceDefaults
- Contracts and Shared libraries
- YARP gateway
- React + Vite + Tailwind + PCDS
- OpenTelemetry configuration
- Correlation/error/event envelope primitives
- Outbox/inbox and reusable relay mechanisms
- Local SQL/Service Bus resources

### Phase 2–8 — Service scaffolding and feature implementation
See [Ongoing Architecture Plan](docs/design/ongoing-architecture-plan.md) for detailed phases.

---

## References

- [ADR-0015: Bounded-Context Catalog](docs/design/adr-0015-bounded-context-catalog.md)
- [Integration Event Catalog](docs/design/integration-event-catalog.md)
- [Product & System Requirements](docs/requirements/lightweight-crm-product-and-system-requirements.md)
- [CLAUDE.md](CLAUDE.md) — Architecture rules and usage patterns
- [High-Level Design](docs/design/high-level-design.md)
- [Ongoing Architecture Plan](docs/design/ongoing-architecture-plan.md)

---

## Approval

- [x] Six-service architecture approved
- [x] Database ownership and naming approved
- [x] Integration event topology approved
- [x] Deployment architecture approved
- [ ] Ready to update CLAUDE.md and begin Phase 1 scaffolding
