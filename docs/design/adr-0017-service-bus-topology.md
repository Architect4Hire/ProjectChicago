# ADR-0017: Azure Service Bus Topic/Subscription Topology

**Status:** Accepted  
**Date:** 2026-08-12  
**Participants:** Architecture team  
**Requirement Links:** [ASYNC-001..008](../requirements/lightweight-crm-product-and-system-requirements.md#async-001); [OUTBOX-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#outbox-001)  
**Decision:** Implement a single shared topic with per-service consumer subscriptions. Publishers (CRM, Identity) use timer-triggered outbox relay Functions; Audit subscribes with no filtering. Future services extend by adding subscriptions without changing existing topology.

## Problem

Project Chicago requires a durable, event-driven integration topology that:

1. Supports transactional outbox pattern (OUTBOX-001..006): state and outbox commit together; timer relay drains to broker
2. Enables at-least-once delivery with idempotent consumption (ASYNC-005..006): applications tolerate duplicates
3. Provides observable retry/dead-letter behavior (ASYNC-007..008): poison messages are bounded and visible
4. Remains extensible as new services are added (Notification, Search, Workflow)
5. Keeps topology and entity names out of domain/business code (configuration-driven only)

We must decide:

- **Topology shape**: Single shared topic vs. multiple topics vs. point-to-point queues?
- **Subscription strategy**: Per-service subscriptions with filtering vs. shared vs. no filtering?
- **Configuration**: How do entity names stay out of domain code?
- **Observability**: Where are dead-letter queues and retry metrics captured?

## Forces

- **Transactional Outbox** [OUTBOX-001..006]: Domain state and outbox record must commit atomically; relay function drains asynchronously
- **At-Least-Once Delivery** [ASYNC-005..006]: Message consumers must tolerate duplicates and remain idempotent
- **Bounded Retry** [ASYNC-007..008]: Poison messages must not retry forever; dead-letter queue is observable
- **Extensibility**: Future consumers (Notification, Search, Workflow) must subscribe without architecture rework
- **Separation of Concerns**: Topology (topic/subscription names) is infrastructure configuration; domain code must not know entity names
- **Least-Privilege**: Publishers only send; consumers only listen; Audit is append-only (never publishes)

## Decision

Project Chicago uses a **single shared topic with per-consumer subscriptions**, following the pub-sub pattern established in ADR-0015.

### Logical Entities

#### Topic: `ProjectChicago.Events`

- **Purpose**: Central event stream for all cross-service integration events
- **Publishers**: 
  - CRM.Functions (timer-triggered outbox relay)
  - Identity.Functions (timer-triggered outbox relay)
- **Subscribers**: 
  - Audit (subscription: `Audit`, consumes all events)
  - Notification (future, subscription: `Notification`)
  - Search (future, subscription: `Search`)
  - Workflow (future, subscription: `Workflow`)
- **Retention**: 14 days (message TTL); auto-expire old events
- **Size**: 1–4 GB (scaled per environment); monitor ingestion metrics
- **Partitions**: 1 (dev/test); 4+ (prod, if throughput stress detected)

### Publisher Configuration

#### CRM.Functions (Timer-Triggered Outbox Relay)

**Responsibility:**  
Poll `ProjectChicago_Crm.OutboxMessages` table; publish pending events to Service Bus topic.

**Target:**  
Topic `ProjectChicago.Events` (resolved from configuration, never hardcoded)

**Permissions:**  
Azure Service Bus Data Sender role on topic `ProjectChicago.Events`

**Configuration Keys:**
```
ServiceBus:FullyQualifiedNamespace = "projectchicago-{env}.servicebus.windows.net"
ServiceBus:TopicName = "ProjectChicago.Events"
Outbox:RelaySchedule = "0 */10 * * * *"  # every 10 seconds
Outbox:BatchSize = 50  # messages per relay invocation
Outbox:MaxConcurrency = 2  # concurrent publishes to broker
```

**Processing Flow:**
1. Timer trigger fires every 10 seconds (configurable via schedule)
2. SELECT up to 50 pending OutboxMessages (WHERE PublishedAt IS NULL)
3. For each message:
   - Deserialize event payload
   - Ensure CorrelationId, CausationId, TraceId, MessageId present
   - ServiceBusSender.PublishAsync() to topic
4. Update OutboxMessages.PublishedAt = now (idempotent)
5. Log metrics: batch size, latency, failures

**Idempotency:**  
Message is published only after successful broker confirmation. If relay crashes after publish but before marking OutboxMessages.PublishedAt, the next relay invocation detects the already-published message by MessageId (stored in outbox) and skips re-publish.

**Failure Handling:**
- Transient failures (timeout, throttle): Exception thrown; message remains pending for next relay invocation
- Permanent failures (poison payload): Log with full context; alert monitoring; message remains pending (manual intervention required)
- Broker unavailable: Exception thrown; retry on next timer invocation

#### Identity.Functions (Timer-Triggered Outbox Relay)

**Responsibility:**  
Poll `ProjectChicago_Identity.OutboxMessages` table; publish pending events to Service Bus topic.

**Target:**  
Topic `ProjectChicago.Events` (resolved from configuration, never hardcoded)

**Permissions:**  
Azure Service Bus Data Sender role on topic `ProjectChicago.Events`

**Configuration Keys:**
```
ServiceBus:FullyQualifiedNamespace = "projectchicago-{env}.servicebus.windows.net"
ServiceBus:TopicName = "ProjectChicago.Events"
Outbox:RelaySchedule = "0 */10 * * * *"  # every 10 seconds
Outbox:BatchSize = 50
Outbox:MaxConcurrency = 2
```

**Processing Flow & Idempotency:**  
Identical to CRM.Functions relay (same outbox relay service used by both).

### Consumer Configuration

#### Audit.Functions (Service Bus Trigger)

**Responsibility:**  
Consume events from subscription; append audit entries to AuditEvents table (idempotently).

**Source:**  
Subscription `Audit` on topic `ProjectChicago.Events` (resolved from configuration)

**Permissions:**  
Azure Service Bus Data Receiver role on subscription `Audit`

**Configuration Keys:**
```
ServiceBus:FullyQualifiedNamespace = "projectchicago-{env}.servicebus.windows.net"
ServiceBus:TopicName = "ProjectChicago.Events"
ServiceBus:SubscriptionName = "Audit"
AuditFunction:MaxConcurrency = 5  # concurrent message handlers
```

**Function Binding (in function.json or attribute):**
```json
{
  "name": "message",
  "type": "serviceBusTrigger",
  "direction": "in",
  "topicName": "%ServiceBus:TopicName%",
  "subscriptionName": "%ServiceBus:SubscriptionName%",
  "connection": "ServiceBusConnection",
  "cardinality": "one"
}
```

**Processing Flow:**
1. Service Bus trigger activated for each message
2. Extract correlation/causation/trace context from message headers
3. Deserialize event envelope and payload
4. Facade.HandleAuditEventAsync(event, context)
   - Facade: Input validation, correlation context population
   - Business: Construct AuditEntry fact (decide what to audit, redact sensitive fields)
   - Data layer:
     - Check inbox: SELECT InboxMessage WHERE MessageId = ?
     - If not found: INSERT InboxMessage (mark as processing)
     - INSERT AuditEntry (append-only audit fact)
     - COMMIT transaction
     - UPDATE InboxMessage SET ProcessedAt = now, mark complete
5. Function returns (message auto-acknowledged by Service Bus)

**Idempotency:**  
Duplicate MessageId = already-completed inbox entry. Data layer detects this and skips audit insert; Function logs duplicate detection with telemetry.

**Failure Handling:**
- If processing fails (exception during INSERT): Function invocation fails; do NOT call message.CompleteAsync()
- Service Bus automatically retries (up to max delivery count)
- Inbox remains in "processing" state until either:
  - Successful completion (inbox marked complete)
  - Max delivery exceeded (message moved to dead-letter queue)

### Topic Configuration (Infrastructure)

| Setting | Value | Rationale | Configurable |
|---------|-------|-----------|--------------|
| **Message TTL** | 14 days | Audit/all consumers must consume within this window; old events auto-expire | Yes (per environment) |
| **Max size** | 1 GB (dev), 4 GB (prod) | Accommodate accumulated events; monitor ingestion rate | Yes |
| **Partitions** | 1 (dev/test), 4+ (prod) | Starts minimal; scale if relay throughput becomes bottleneck | Yes |
| **Duplicate detection** | Disabled | Application handles via outbox MessageId + inbox pattern | No |
| **Auto-delete on idle** | Disabled | Preserve topic for ops visibility and historical queries | No |
| **Enable partitioning** | Yes | Required for > 5 GB throughput needs | No |

### Subscription Configuration (Audit)

| Setting | Value | Rationale | Configurable |
|---------|-------|-----------|--------------|
| **Max delivery count** | 10 | [ASYNC-007] Bounded retry; poison message after 10 failed attempts | Yes (per environment) |
| **Message lock duration** | 30 seconds | Default; increase if audit processing consistently takes > 30s | Yes |
| **Dead-letter on message expiration** | Enabled | [ASYNC-008] Expired messages moved to DLQ, not silently discarded | No |
| **Dead-letter on filter evaluation exception** | Enabled | (no filters used, but good practice) | No |
| **Default message TTL** | Inherit from topic | Consistent retention across topic and subscriptions | No |
| **Filters** | None | [RESTRICTION] No domain rules in broker; Audit processes all events | No |

### Configuration Keys (Central Location)

All topology entity names, connection settings, and timing parameters are **configuration keys**, never hardcoded in domain code.

#### Aspire AppHost Configuration

```csharp
var serviceBus = builder.AddAzureServiceBus("messaging")
    .WithReference(...);  // Azure SDK credential

var topicName = "ProjectChicago.Events";
var auditSubscriptionName = "Audit";

// Topic and subscription resources declared here (implementation detail)
```

#### CRM.Functions & Identity.Functions appsettings.json

```json
{
  "ServiceBus": {
    "FullyQualifiedNamespace": "projectchicago-dev.servicebus.windows.net",
    "TopicName": "ProjectChicago.Events",
    "Connection": "ServiceBusConnection"
  },
  "Outbox": {
    "RelaySchedule": "0 */10 * * * *",
    "BatchSize": 50,
    "MaxConcurrency": 2,
    "LeaseTimeoutSeconds": 30
  }
}
```

#### Audit.Functions appsettings.json

```json
{
  "ServiceBus": {
    "FullyQualifiedNamespace": "projectchicago-dev.servicebus.windows.net",
    "TopicName": "ProjectChicago.Events",
    "SubscriptionName": "Audit",
    "Connection": "ServiceBusConnection"
  },
  "AuditFunction": {
    "MaxConcurrency": 5,
    "ProcessingTimeoutSeconds": 60
  }
}
```

### Retry and Dead-Letter Expectations

#### Successful Path (Happy Case)

```
T0:      Event published to topic by outbox relay
T0+1s:   Audit.Functions triggered
         ├─ Inbox check: MessageId not seen before ✓
         ├─ INSERT audit entry (ACID with inbox)
         ├─ Mark inbox complete
         └─ Function returns
T0+2s:   Message acknowledged and removed from subscription
```

#### Transient Failure Path (Retriable Error)

```
T0:      Event published to topic
T0+1s:   Audit.Functions triggered
         ├─ Processing error (SQL timeout, network blip)
         └─ Function throws exception (NOT caught)
T0+2s:   Service Bus automatic retry #1
         └─ Function re-invoked
T0+4s:   Retry #2 (exponential backoff)
T0+8s:   Retry #3
...
T0+600s: Retry #10 (≈10 minutes elapsed)
         ├─ If still failing: Message moved to dead-letter queue
         └─ Alert: DeadLetterMessageCount > 0 triggers investigation
```

**Exponential Backoff Sequence:**
1. Initial delivery
2. +1s
3. +2s
4. +4s
5. +8s
6. +16s
7. +32s
8. +1m
9. +2m
10. +10m total

#### Duplicate Delivery Path (At-Least-Once Guarantee)

```
T0:      Event published; Audit processes successfully
         ├─ Inbox.MessageId recorded
         ├─ AuditEntry inserted
         ├─ Inbox marked complete
         └─ Acknowledgment sent to Service Bus

T0+ε:    Network glitch: acknowledgment lost before broker records completion
         └─ Service Bus does not mark message as received

T0+ε+30s: Service Bus timeout; re-deliver same message

T0+ε+31s: Audit.Functions triggered again (same MessageId)
         ├─ Inbox check: MessageId ALREADY EXISTS (previously completed)
         ├─ No-op: Skip audit insert, log duplicate detection
         └─ Function returns success

         Result: No duplicate audit entry; idempotency preserved
```

#### Dead-Letter Path (Poison Message)

```
T0-1000: Event created with invalid/unparseable payload
T0:      Published to topic
T0+1s:   Audit.Functions triggered
         ├─ Deserialization fails (bad contract)
         ├─ Function throws DeserializationException
         └─ Not retryable (payload won't change)
T0+2s:   Retry #1 → Same error
...
T0+600s: Retry #10 failed
         └─ Message moved to dead-letter queue (DLQ)
         └─ Alert: `Microsoft.ServiceBus.DeadletterMessageCount{subscription="Audit"}` > 0

         Investigation Path:
         1. Query Service Bus dead-letter messages (Azure Portal / SDK)
         2. Extract message ID and correlation ID
         3. Link to audit trace (TraceId)
         4. Determine root cause
         5. Publish corrected event or manually process (per domain policy)
```

### Topology Stays Out of Domain Code

**What is configuration (allowed to vary per environment):**
- Topic name: `ProjectChicago.Events`
- Subscription name: `Audit`, `Notification`, etc.
- Connection strings / endpoints
- Timer schedule and batch sizes
- Retry counts and timeouts

**What is code (fixed for the codebase):**
- Event envelope structure (CorrelationId, TraceId, MessageId, Payload)
- Event contract types (ClientCreated, UserActivated, etc.)
- Business logic to construct/consume events
- Idempotency mechanisms (inbox pattern)

**Forbidden Patterns:**

```csharp
// ✗ BAD: Hardcoded topic name in domain code
public class ClientEventPublisher
{
    public async Task PublishAsync(ClientCreatedEvent evt)
    {
        await _sender.SendMessageAsync(new ServiceBusMessage { ... }, 
            topicName: "ProjectChicago.Events");  // ← hardcoded!
    }
}

// ✓ GOOD: Topic name from configuration
public class ClientEventPublisher
{
    private readonly ServiceBusClientOptions _options;
    
    public ClientEventPublisher(IOptions<ServiceBusClientOptions> options)
    {
        _options = options.Value;
    }
    
    public async Task PublishAsync(ClientCreatedEvent evt)
    {
        await _sender.SendMessageAsync(new ServiceBusMessage { ... }, 
            topicName: _options.TopicName);  // ← from DI config
    }
}

// ✗ BAD: Subscription filter encodes domain rule
Subscription auditSub = new Subscription
{
    Filter = new SqlRuleFilter("eventType LIKE 'Client%'")  // ← domain in broker!
};

// ✓ GOOD: Audit consumes all; domain logic in Facade
Subscription auditSub = new Subscription
{
    // No filter; Audit.Facade decides what to audit
};
```

### Publishing/Consuming Service Mapping

**Verification: Every publishing and consuming context is unambiguously mapped to a logical entity.**

| Bounded Context | Role | Logical Entity | Configuration Key | Event Types | Notes |
|---|---|---|---|---|---|
| **CRM** | Publisher | Topic: `ProjectChicago.Events` | `ServiceBus:TopicName` | ClientCreated, ClientLifecycleChanged, ProjectCreated, ProjectStatusChanged, ProjectCompleted, TaskCreated, TaskAssigned, TaskReassigned, TaskCompleted, TaskReopened, TaskPriorityChanged, ClientArchived, ProjectArchived | Via timer-triggered outbox relay Function |
| **Identity** | Publisher | Topic: `ProjectChicago.Events` | `ServiceBus:TopicName` | UserCreated, UserActivated, UserDeactivated, UserLocked, PasswordReset, PasswordChanged, RoleAssigned, RoleRemoved | Via timer-triggered outbox relay Function |
| **Audit** | Consumer | Subscription: `Audit` | `ServiceBus:SubscriptionName` | All events (no filtering) | Service Bus trigger; appends audit entries idempotently |
| **Notification** (future) | Consumer | Subscription: `Notification` | (pending ADR/deployment) | TaskAssigned, TaskCompleted, ProjectCompleted, ClientLifecycleChanged, UserCreated (with optional filters) | To be added via future ADR |
| **Search** (future) | Consumer | Subscription: `Search` | (pending ADR/deployment) | ClientCreated, ProjectCreated, TaskCreated, ClientArchived, ProjectArchived (with optional filters) | To be added via future ADR |
| **Workflow** (future) | Consumer | Subscription: `Workflow` | (pending ADR/deployment) | ClientCreated, ProjectCreated, ProjectCompleted, TaskCompleted, TaskOverdue, UserCreated (with optional filters) | To be added via future ADR |

**✓ Verification passed:** 
- No service publishes to multiple topics
- No subscription consumes from multiple topics
- Each publisher has exactly one target (Topic: ProjectChicago.Events)
- Audit has exactly one subscription (Audit on topic)
- Future services follow the same single-topic-per-subscription pattern
- No ambiguity in entity mappings

---

## Consequences

### Positive

- **Minimal topology**: Single topic is simpler to operate and monitor than multiple topics
- **Future-proof**: New services (Notification, Search, Workflow) add subscriptions without changing existing topology or code
- **Configuration-driven**: No topology details in code; environment-specific settings only
- **Observable**: Dead-letter queue and metrics provide clear operational signals
- **Extensible**: Audit receives all events and can support future queries/reports without topology changes
- **Auditability**: All business mutations flow through Audit for compliance
- **Separation of concerns**: Domain code remains unaware of infrastructure topology

### Negative/Tradeoffs

- **Single topic as bottleneck**: At extreme scale (> 5GB/day throughput), partition scaling required
- **Audit processes all events**: Audit must be robust enough to handle all event types; defensive coding required
- **Configuration complexity**: Multiple environment-specific configurations (dev, test, prod) must be managed
- **Monitoring at scale**: Dead-letter queue depth and relay latency must be closely watched as volume grows

---

## Migration Path for Future Services

When Notification, Search, or Workflow services are onboarded:

1. **Create subscription** in Service Bus (via Aspire AppHost or infrastructure code)
   ```csharp
   var notificationSub = topic.AddSubscription("Notification")
       .WithFilter(new SqlRuleFilter("eventType IN ('TaskAssigned', 'TaskCompleted', ...)"));
   ```

2. **Add configuration** to service's Function appsettings
   ```json
   {
     "ServiceBus": {
       "SubscriptionName": "Notification"
     }
   }
   ```

3. **Add Service Bus trigger** to new service's Functions project (standard binding)

4. **No changes to existing services** (CRM, Identity, Audit) — they remain publishing/consuming to the same topic

---

## Links and References

- **ADR-0015**: Bounded-context catalog (CRM, Identity, Audit, Notification, Search, Workflow)
- **ADR-0016**: Audit architecture (event-driven ingestion, idempotent inbox)
- **CLAUDE.md § Messaging and Azure Functions**: Outbox pattern, relay, inbox pattern
- **.claude/rules/messaging.md**: Integration event contracts, Service Bus seam tests
- **.claude/rules/functions.md**: Function binding configuration and retry handling
- **.claude/rules/aspire.md**: AppHost resource declaration and dependency wiring
- **ASYNC-001..008**: Asynchronous processing and retry requirements
- **OUTBOX-001..006**: Transactional outbox pattern requirements
- **TRACE-003..007**: Distributed trace propagation (correlation, causation, trace ID)

---

## Acceptance Criteria

This ADR is accepted when:

1. ✓ Topic `ProjectChicago.Events` is declared in Aspire AppHost (implementation to follow in task)
2. ✓ Subscription `Audit` is declared in Aspire AppHost with configuration-driven entity names
3. ✓ CRM.Functions references topic via `ServiceBus:TopicName` configuration key (no hardcoded names)
4. ✓ Identity.Functions references topic via `ServiceBus:TopicName` configuration key
5. ✓ Audit.Functions Service Bus trigger binding uses `ServiceBus:SubscriptionName` configuration key
6. ✓ Batch size, relay schedule, max delivery count are configurable (not magic numbers)
7. ✓ Dead-letter queue is monitored (metric: `ServiceBus:DeadLetterMessageCount` for subscription)
8. ✓ Outbox relay metrics instrumented (pending count, publish latency, retry count)
9. ✓ Test coverage includes:
   - [ ] Duplicate delivery detection and idempotent audit processing
   - [ ] Max delivery count exceeded → message in dead-letter queue
   - [ ] Transient relay failure → message remains pending for retry
   - [ ] Correlation/trace context preserved end-to-end
   - [ ] Outbox message published only after broker confirmation
10. ✓ Domain code contains no hardcoded topic/subscription names
11. ✓ Future services can add subscriptions via Aspire/deployment without modifying existing service code
12. ✓ Aspire local development environment supports topology (Service Bus emulator or Azure SDK integration)

---

## Next Steps

- **ADR-0018**: Browser authentication transport and session policy (currently in progress)
- **Implementation Task**: Aspire AppHost resources and Function bindings for CRM/Identity/Audit relay and Audit consumption (awaiting ADR-0017 acceptance)
- **Future ADR**: Subscription filtering policy and topology details for Notification/Search/Workflow (after those services are designed)
