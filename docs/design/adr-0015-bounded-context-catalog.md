# ADR-0015: Initial Bounded-Context Catalog

**Status:** Proposed  
**Date:** 2026-08-12  
**Participants:** Architecture team  
**Decision:** Accept the six-service initial catalog for Project Chicago  

## Problem

Project Chicago requires clear service boundaries aligned with the lightweight, client-centric product scope while supporting enterprise-grade audit, observability, and reliability. The CLAUDE.md intentionally left the bounded-context catalog undefined.

We must decide:
- How many initial services?
- What domain does each own?
- How do they integrate?
- What are the persistence/deployment boundaries?

## Forces

- **Lightweight scope** [PR-001]: Avoid premature service proliferation
- **Strong ownership** [DATA-031]: Each service owns exactly one database
- **Auditability** [AUDIT-001..008]: Every mutation must be traceable and append-only
- **Future extensibility**: Must not block reasonable growth (Notifications, Search, Workflow)
- **Deployment independence**: Each service independently deployable on Flex Consumption
- **Traceability** [TRACE-003..007]: Distributed traces must correlate across service boundaries

## Decision

Project Chicago shall launch with **six bounded contexts**, each with HTTP host, .Core implementation, .Functions async entry points, and one dedicated SQL database:

### 1. **CRM Service**
**Projects:** `ProjectChicago.Crm`, `ProjectChicago.Crm.Core`, `ProjectChicago.Crm.Functions`  
**Database:** `projectchicago_crm`

**Responsibilities:**
- Own Client, Project, Task entities and their lifecycle/status transitions
- Client detail aggregation (Client + Projects + Tasks)
- Client/Project/Task search, filtering, pagination
- Assignment management (assign/reassign/unassign)
- Dashboard summaries over CRM-owned data
- Authorization scoping for CRM resources

**Integration:**
- **Publishes:** ClientCreated, ClientLifecycleChanged, ProjectCreated, ProjectStatusChanged, ProjectCompleted, TaskCreated, TaskAssigned, TaskReassigned, TaskCompleted, TaskReopened, TaskPriorityChanged, ClientArchived, ProjectArchived
- **Consumes:** (none initially)
- **HTTP callers:** Notification Service (for notification rules), Search Service (for indexing), Workflow Service (for triggering)
- **Called by:** React gateway, internal audit lookups

**Functions:**
- Timer-triggered outbox relay (drain CRM transactional outbox to Service Bus)

---

### 2. **Identity Service**
**Projects:** `ProjectChicago.Identity`, `ProjectChicago.Identity.Core`, `ProjectChicago.Identity.Functions`  
**Database:** `projectchicago_identity`

**Responsibilities:**
- ASP.NET Core Identity implementation (users, roles, claims, password hashing)
- User account lifecycle (creation, activation, deactivation, lockout, password reset)
- Role and claims management
- User/role administration endpoints
- Authentication events and logging

**Integration:**
- **Publishes:** UserCreated, UserActivated, UserDeactivated, UserLocked, PasswordReset, PasswordChanged, RoleAssigned, RoleRemoved
- **Consumes:** (none initially)
- **HTTP callers:** All services (to resolve user details), gateway (for auth checks)

**Functions:**
- Timer-triggered outbox relay

---

### 3. **Audit Service**
**Projects:** `ProjectChicago.Audit`, `ProjectChicago.Audit.Core`, `ProjectChicago.Audit.Functions`  
**Database:** `projectchicago_audit`

**Responsibilities:**
- Append-only audit trail (immutable by design)
- Consume and persist audit events from all publishing services
- Provide read-only query API (audit history, activity timeline)
- Link audit entries to distributed traces via Trace ID
- Redact sensitive values (passwords, tokens, etc.)
- Maintain inbox for idempotent event processing

**Integration:**
- **Publishes:** (none)
- **Consumes:** All events from CRM, Identity, Notification, Search, Workflow services via Service Bus subscriptions
- **HTTP callers:** CRM, React gateway (for audit history views)

**Functions:**
- Service Bus trigger(s) for event consumption and audit append
- (No timer-triggered outbox relay; Audit is append-only)

---

### 4. **Notification Service**
**Projects:** `ProjectChicago.Notification`, `ProjectChicago.Notification.Core`, `ProjectChicago.Notification.Functions`  
**Database:** `projectchicago_notification`

**Responsibilities:**
- Consume CRM and Identity events
- Evaluate notification rules (task assignment, deadline approaching, project milestone, status change, activity digest)
- Send notifications via configured channels (in-app, email, webhooks)
- Maintain notification history and delivery status
- Notification preferences and opt-out management
- Provide notification API for settings/history queries

**Integration:**
- **Publishes:** NotificationSent, NotificationFailed, NotificationDelivered
- **Consumes:** TaskAssigned, TaskCompleted, TaskReopened, ProjectCompleted, ProjectStatusChanged, ClientLifecycleChanged, UserCreated, etc.
- **HTTP callers:** CRM (query notification status), React gateway (settings/preferences)

**Functions:**
- Service Bus trigger(s) for event consumption and rule evaluation
- Timer-triggered outbox relay

---

### 5. **Search Service**
**Projects:** `ProjectChicago.Search`, `ProjectChicago.Search.Core`, `ProjectChicago.Search.Functions`  
**Database:** `projectchicago_search`

**Responsibilities:**
- Maintain denormalized read-model of CRM data (searchable index)
- Global search across Clients, Projects, Tasks
- Optimized full-text search queries
- Filter and faceting on CRM entities
- Eventual consistency (event-driven synchronization)
- Provide search API (query, autocomplete, advanced filters)

**Integration:**
- **Publishes:** SearchIndexUpdated (for observability; optional)
- **Consumes:** ClientCreated, ClientLifecycleChanged, ProjectCreated, ProjectStatusChanged, TaskCreated, TaskAssigned, TaskCompleted, ClientArchived, ProjectArchived
- **HTTP callers:** React gateway, CRM (for global search results)

**Functions:**
- Service Bus trigger(s) for event consumption and index update
- (No timer-triggered outbox relay; Search is read-only, denormalization only)

---

### 6. **Workflow Service**
**Projects:** `ProjectChicago.Workflow`, `ProjectChicago.Workflow.Core`, `ProjectChicago.Workflow.Functions`  
**Database:** `projectchicago_workflow`

**Responsibilities:**
- Define and manage workflow automation rules (stored templates)
- Consume CRM and Identity events
- Evaluate rules and trigger automated actions
- Orchestrate workflows across service boundaries (publish secondary events)
- Maintain execution history and audit trail
- Provide workflow administration API (define rules, query executions, adjust templates)
- Handle compensation/rollback for failed workflow steps

**Integration:**
- **Publishes:** WorkflowTriggered, WorkflowExecuted, WorkflowFailed, WorkflowCompensated, [dynamic actions based on rule definitions]
- **Consumes:** ClientCreated, ProjectCreated, ProjectCompleted, TaskCompleted, TaskOverdue, UserCreated, etc.
- **HTTP callers:** CRM (for workflow-triggered actions), React gateway (admin/settings)

**Functions:**
- Service Bus trigger(s) for event consumption and rule evaluation
- Timer-triggered outbox relay

---

## Integration Topology

### Service Bus Topics and Subscriptions

```
Topic: ProjectChicago.Events

Subscriptions:
  - Audit (all events, no filtering)
  - Notification (filters: Task.*, Project.*, Client.*, User.*)
  - Search (filters: Client.*, Project.*, Task.*)
  - Workflow (filters: Client.*, Project.*, Task.*, User.*)
```

### Synchronous Communication

Only through HTTP APIs via YARP gateway:
- CRM → Identity: resolve user details
- CRM → Audit: fetch audit history
- Notification → CRM: query notification rules/context
- Search → CRM: initial indexing context
- Workflow → CRM: fetch entity state for rule evaluation

### Event Publishing Pattern

All services use transactional outbox + timer-triggered relay:
1. **Service Core** emits domain facts during transaction
2. **Data layer** persists domain state + outbox record atomically
3. **Timer Function** (once per service):
   - Leases pending outbox batch
   - Publishes to Service Bus with correlation/trace context
   - Marks messages published (idempotent)
4. **Service Bus** delivers at-least-once
5. **Consuming Service Functions** process with inbox idempotency

### Event Versioning and Stability

- Event names describe facts in past tense (ClientCreated, not CreateClient)
- Events are external contracts; breaking changes require versioning
- Payload includes: EventId, EventType, Version, OccurredUtc, CorrelationId, CausationId, TraceId, ActorId
- Sensitive values (passwords, tokens, etc.) never included

---

## Deployment Architecture

Each service is independently deployed on **Azure Functions Flex Consumption** (Functions) + **Azure App Service or Container Instances** (HTTP host):

```
ProjectChicago.Crm              → projectchicago-crm-app + projectchicago-crm-func
ProjectChicago.Identity         → projectchicago-identity-app + projectchicago-identity-func
ProjectChicago.Audit            → projectchicago-audit-app + projectchicago-audit-func
ProjectChicago.Notification     → projectchicago-notification-app + projectchicago-notification-func
ProjectChicago.Search           → projectchicago-search-app + projectchicago-search-func
ProjectChicago.Workflow         → projectchicago-workflow-app + projectchicago-workflow-func
```

Each service:
- Has its own SQL database
- Connects to shared Service Bus (topic subscriptions scoped by service)
- Exports OpenTelemetry to shared Application Insights
- Uses Managed Identity for Azure resource access (least-privilege)

---

## Rationale

### Why Six Services?

1. **CRM** — Core business domain; owns Clients/Projects/Tasks lifecycle
2. **Identity** — Cross-cutting authentication/authorization; separate lifecycle from CRM (user management ≠ CRM operations)
3. **Audit** — Regulatory/compliance requirement; append-only immutability critical; separate from services that mutate
4. **Notification** — Consumption of events drives outbound communication; independent of CRM domain rules
5. **Search** — Denormalized read-model; eventual consistency vs. transactional consistency; independent indexing lifecycle
6. **Workflow** — Automation orchestration and rule engine; independent execution lifecycle; consumes events from other services

### Why Not Fewer?

- **CRM + Identity combined:** Would require Identity to know about business objects (Projects, Tasks) or vice versa; violates single responsibility
- **No separate Audit:** Risks audit mutation through bugs in services; compliance requirement demands immutability guarantee

### Why Not More?

- **No Reporting:** Dashboards and reports query existing APIs (CRM Search API, Audit API) with appropriate aggregation
- **No Notifications subsystem as separate from Notification Service:** Notification Service handles both rules and delivery
- **No separate Orchestration engine:** Workflow Service handles orchestration

---

## Constraints and Invariants

1. **No cross-database queries** — HTTP APIs only for cross-service data
2. **One database per service** — Persistence boundary = service boundary
3. **Service Bus is the async boundary** — No direct HTTP polling or async calls between services (except for read-only queries)
4. **All mutations publish events** — Every change to CRM, Identity, Workflow state must eventually reach Audit
5. **Audit is append-only** — No UPDATE/DELETE on audit records in normal application workflows
6. **Identity is authoritative** — User details resolved through Identity API, not cached/duplicated in other services
7. **Correlation IDs flow through** — Every request/event carries TraceId, CorrelationId, CausationId from cradle to grave

---

## Future Evolution

This catalog is stable for:
- Single-organization lightweight CRM use cases
- Supporting Users, Clients, Projects, Tasks, and automation

The catalog can extend to:
- **Reporting Service** (if BI/analytics demands exceed API aggregation)
- **Multi-tenancy Service** (if customer/workspace isolation is added)
- **Integrations Service** (if third-party webhooks/sync is added)

New services require an ADR and evidence that they have:
- Independent lifecycle/deployment needs
- Separate data ownership
- Clear boundary from existing services

---

## Acceptance Criteria

- [ ] All six services scaffolded with HTTP host, .Core, .Functions structure
- [ ] Six SQL databases created with logical names and ownership documented
- [ ] Service Bus topic and subscriptions configured per topology section
- [ ] Correlation/trace context flows through all synchronous and asynchronous paths
- [ ] Each service's integration event contract validated for version, payload, sensitive-value redaction
- [ ] Distributed trace correlation tested end-to-end (request → CRM → Audit consumption)
- [ ] Audit immutability verified (no mutation paths from application code)
- [ ] Search denormalization tested for eventual consistency
- [ ] Notification rule evaluation tested against sample events
- [ ] Workflow rule execution tested with compensation scenarios
- [ ] CLAUDE.md updated with confirmed service boundaries, database naming, event topology

---

## References

- [PR-001..006: Product Principles](../requirements/lightweight-crm-product-and-system-requirements.md#pr-001)
- [DATA-030..034: Data Storage](../requirements/lightweight-crm-product-and-system-requirements.md#data-030)
- [SEC-001..016: Security](../requirements/lightweight-crm-product-and-system-requirements.md#sec-001)
- [AUDIT-001..008: Business Audit](../requirements/lightweight-crm-product-and-system-requirements.md#audit-001)
- [TRACE-001..007: Traceability](../requirements/lightweight-crm-product-and-system-requirements.md#trace-001)
- [ASYNC-001..008: Asynchronous Processing](../requirements/lightweight-crm-product-and-system-requirements.md#async-001)
- [OUTBOX-001..006: Transactional Outbox](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-001)
- [DEPLOY-004: Azure Functions Flex Consumption](../requirements/lightweight-crm-product-and-system-requirements.md#deploy-004)
