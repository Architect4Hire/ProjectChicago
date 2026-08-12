# ADR-0021: OpenTelemetry Instrumentation and Azure Monitor Observability

**Status:** Accepted  
**Date:** 2026-08-12  
**Participants:** Architecture team  
**Requirement Links:** [TRACE-001..007](../requirements/lightweight-crm-product-and-system-requirements.md#trace-001); [OTEL-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#otel-001); [OBS-001..005](../requirements/lightweight-crm-product-and-system-requirements.md#obs-001); [LOG-001..006](../requirements/lightweight-crm-product-and-system-requirements.md#log-001); [OPS-001..004](../requirements/lightweight-crm-product-and-system-requirements.md#ops-001)  
**Decision:** Establish OpenTelemetry as the instrumentation standard for all APIs, services, and Azure Functions. Azure Monitor/Application Insights is the production single pane of glass. W3C Trace Context propagates across gateway, services, databases, Service Bus, and Functions. Logs are structured and automatically correlated to traces. Business-safe identifiers (Client/Project/Task IDs) are attached without customer payloads.

## Problem

Project Chicago is a distributed system with six bounded services, asynchronous integration through Service Bus, timer-triggered Functions, and SQL databases. An HTTP request may:

1. Arrive at YARP gateway
2. Route to CRM API host
3. Execute a domain transaction
4. Save domain state + outbox record to SQL
5. Return response to browser
6. (Asynchronously) Timer-triggered Function drains outbox → publishes to Service Bus
7. Audit.Functions consumes event → appends to SQL
8. Notification.Functions consumes event → sends notifications
9. Search.Functions consumes event → updates denormalized index

An operator must be able to:

- Start with a single API request or trace ID
- Follow all downstream work (including asynchronous) through the system
- Identify which service, database, HTTP call, or Service Bus operation failed
- Correlate logs to traces to diagnose root causes
- Determine whether the system is healthy

We must decide:

1. **Instrumentation standard**: How do we collect traces, metrics, and logs consistently across all services?
2. **Trace propagation**: How does trace context flow through async work (outbox → relay → Service Bus → consumer Function)?
3. **Observability backend**: Which system is the single pane of glass for operations?
4. **Log-trace correlation**: How are logs automatically linked to traces without manual instrumentation?
5. **Sensitive data**: How do we attach business identifiers (Client/Project/Task IDs) without logging customer PII?
6. **Sampling**: Who decides trace sampling and at what rates?
7. **Health**: How do operators distinguish application, dependency, and readiness health?

## Forces

- **Distributed Complexity** [TRACE-001..007]: Requests spawn multiple services, Functions, databases, and message operations; single traces must correlate all
- **Asynchronous Work** [TRACE-004]: HTTP request → outbox → timer relay → Service Bus → consumer Function creates separate invocation contexts that must remain linked
- **Observability as Baseline** [OTEL-001..006, OBS-001..005]: Every API, service, and Function must participate in traces/metrics/logs from day one, not bolted on later
- **Structured Logging** [LOG-001..006]: Production behavior depends on structured fields, not log parsing; logs must correlate to traces automatically
- **Business Diagnostics** [TRACE-006]: Operators need to filter telemetry by Client/Project/Task to answer "what happened to this client?"
- **Operational Alerts** [OPS-001..004]: Thresholds (error rates, latency, dead-letter depth) must be configurable without code changes
- **Local Development** [OBS-001]: Developers must investigate distributed traces and metrics locally during Aspire-based development

## Decision

### 1. OpenTelemetry as Instrumentation Standard

**OpenTelemetry** is the standard for all telemetry collection in Project Chicago.

- **Traces**: Distributed request tracing with automatic span creation for requests, dependencies, and operations
- **Metrics**: Application metrics (request rate, latency, exception rate, Service Bus processing, outbox backlog)
- **Logs**: Structured logs with automatic trace context (Trace ID, Span ID) and log levels

**Why OpenTelemetry:**
- Vendor-neutral; no lock-in to Azure
- Industry-standard for distributed tracing
- Automatic instrumentation for ASP.NET Core, HTTP clients, SQL, Service Bus
- Supports W3C Trace Context specification
- Language/platform agnostic for future interoperability
- Broad ecosystem (exporters, collectors, backends)

### 2. Azure Monitor/Application Insights as Production SPOG

**Azure Application Insights** (via Azure Monitor) is the centralized observability backend for all production telemetry.

- All APIs, Functions, and services export traces, metrics, and logs to Application Insights
- Unified query/dashboard interface for operators
- Integration with Azure alerts and diagnostics
- Supports sampling, retention policies, and cost controls

**Local Development:** Aspire Dashboard provides real-time traces/metrics during development (no cloud export required).

### 3. W3C Trace Context Propagation

**W3C Trace Context** (https://www.w3.org/TR/trace-context/) defines the standard headers for trace propagation across services and asynchronous boundaries.

#### Request Arrives at Gateway

```
Browser HTTP Request → YARP Gateway
├─ If no trace header present:
│  └─ YARP generates new TraceId (globally unique)
│  └─ Creates initial SpanId
└─ If trace header present:
   └─ YARP preserves TraceId and propagates
   └─ Creates new SpanId (child of parent)

Response Headers:
├─ traceparent: {version}-{TraceId}-{SpanId}-{TraceFlags}
│  Example: 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01
└─ tracestate: vendor-specific state (optional)
```

#### Propagation Through Sync Operations (HTTP → API → SQL)

```
YARP Ingress (SpanId: A)
├─ traceparent: 00-{TraceId}-A-01
│
└─ HTTP Call to CRM API (SpanId: B, Parent: A)
   ├─ traceparent: 00-{TraceId}-B-01
   │
   └─ SQL Query (Span: C, Parent: B)
      ├─ Activity.Current provides trace context to SQL driver
      └─ SQL dependency recorded with same TraceId
```

#### Propagation Through Async Operations (Outbox → Service Bus → Function)

```
HTTP Request (TraceId: T1, SpanId: S1)
├─ INSERT state + OutboxMessage (trace context T1/S1 in span attributes)
│
└─ Timer-Triggered Relay Function (new invocation, TraceId: T2)
   ├─ SELECT pending OutboxMessages
   ├─ Restore correlation/causation from OutboxMessage metadata
   │  ├─ CorrelationId = from original HTTP request (links business flow)
   │  ├─ CausationId = OutboxMessage.MessageId (links this event to its cause)
   │  └─ OriginalTraceId = T1 (links operational flow back to origin)
   │
   └─ ServiceBusSender.PublishAsync()
      ├─ Message headers include:
      │  ├─ traceparent (T2 from relay invocation)
      │  ├─ Custom headers: correlation-id, causation-id, original-trace-id
      │  └─ Service Bus propagates to consumer
```

#### Propagation in Service Bus Consumer Function

```
Service Bus Message (Headers: correlation-id, original-trace-id, causation-id)
│
└─ Audit.Functions Service Bus Trigger (new invocation, TraceId: T3)
   ├─ Extract correlation/causation/original-trace-id from message headers
   ├─ Link Activity.Current to original trace (TRACE-004: remain correlated)
   │  └─ TraceLink created: T3 linked to T1 (the originating HTTP request)
   │
   └─ Data Layer: INSERT audit entry
      ├─ AuditEntry.CorrelationId = correlation-id (from message)
      ├─ AuditEntry.OriginalTraceId = T1 (from message)
      └─ Stored in AuditDb for audit trail correlation
```

**Result:** Operator can query Application Insights with TraceId=T1 and see:
- Initial HTTP request (YARP → API)
- SQL transactions
- Outbox record insertion
- Timer relay invocation (T2, linked to T1 via causation chain)
- Service Bus publication
- Audit.Functions invocation (T3, linked to T1)
- Audit INSERT operation

### 4. Correlation and Causation Metadata

Every integration event carries metadata to link asynchronous operations back to their origin:

```csharp
// OutboxMessage stored with correlation metadata
{
  MessageId: Guid.NewGuid(),           // Unique ID for this event
  CorrelationId: "corr-ABC123",        // End-to-end business flow (from HTTP request)
  CausationId: "req-XYZ789",           // Parent request/message ID (what caused this?)
  OriginalTraceId: "4bf92f35...",      // W3C TraceId from originating HTTP request
  OccurredAt: DateTime.UtcNow,
  EventPayload: { ... }
}

// When relay publishes to Service Bus, it includes these in message headers:
{
  "correlation-id": "corr-ABC123",
  "causation-id": "req-XYZ789",
  "original-trace-id": "4bf92f35...",
  "message-id": "msg-DEF456",
  "traceparent": "00-4bf92f35...-{relaySpanId}-01"
}

// Consumer Function extracts and uses:
var correlationId = message.ApplicationProperties["correlation-id"];
var originalTraceId = message.ApplicationProperties["original-trace-id"];

// Links Activity.Current to original trace
var activityLink = new ActivityLink(
  new ActivityContext(originalTraceId, default, default));
```

**Result:** From Audit database:
```sql
SELECT * FROM AuditEvents WHERE CorrelationId = 'corr-ABC123';
```

Returns all audit events created by the same business operation, even though they occurred in different services/Functions with different TraceIds.

### 5. Service Naming and Resource Attributes

All telemetry includes consistent resource attributes for filtering and aggregation.

#### Resource Attributes (Set Once During Initialization)

```csharp
// Applied to all traces/metrics/logs from a service
var resource = ResourceBuilder.CreateDefault()
    .AddService(
        serviceName: "projectchicago-crm",        // OTEL-005
        serviceVersion: "1.0.0",                  // Deployed version
        serviceInstanceId: Environment.MachineName)  // VM/container identifier
    .AddAttributes(new Dictionary<string, object>
    {
        ["deployment.environment"] = environment,  // dev, staging, prod
        ["service.namespace"] = "projectchicago",
        ["service.owner"] = "crm-team",
        ["telemetry.sdk.name"] = "opentelemetry",
        ["telemetry.sdk.version"] = "1.x.x"
    });
```

#### Span Attributes (Per Operation)

Business operation spans include additional context:

```csharp
using var activity = new Activity("Client.Create").Start();

activity.SetAttribute("http.method", "POST");
activity.SetAttribute("http.url", "/api/clients");
activity.SetAttribute("http.status_code", 201);
activity.SetAttribute("http.client_ip", clientIp);

// OTEL-003: Business identifiers (safe to log)
activity.SetAttribute("client.id", clientId);  // UUID, safe
activity.SetAttribute("user.id", userId);      // Authenticated user, safe

// RESTRICTION: Never log sensitive data
// ✗ activity.SetAttribute("client.email", "...@example.com");  // PII
// ✓ activity.SetAttribute("client.email_domain", "example.com"); // Sanitized

activity.SetAttribute("duration_ms", stopwatch.ElapsedMilliseconds);
activity.SetAttribute("db.system", "mssql");
activity.SetAttribute("db.statement", parameterizedQuery);  // Sanitized
```

#### Naming Conventions

| Attribute | Format | Example | Usage |
|-----------|--------|---------|-------|
| **Service Name** | `projectchicago-{service}` | `projectchicago-crm`, `projectchicago-audit` | Resource; filtering by service |
| **Span Name** | `{Entity}.{Action}` | `Client.Create`, `Project.UpdateStatus`, `Outbox.Publish` | Business operation tracking |
| **HTTP Route** | Route pattern (not path with IDs) | `/api/clients`, `/api/clients/{id}`, `/api/clients/{id}/projects` | Cardinality; aggregate by route |
| **Database Name** | Fully qualified name | `projectchicago_crm`, `projectchicago_audit` | Dependency tracking |
| **Event Type** | Entity + past tense | `ClientCreated`, `ProjectStatusChanged` | Event filtering in logs/traces |
| **Function Name** | `{Service}.{Trigger}.{Handler}` | `Crm.Timer.OutboxRelay`, `Audit.ServiceBus.EventConsumer` | Function identification |

### 6. Log-Trace Correlation

Logs are **structured** (not free-text) and automatically include trace context when a trace is active.

#### Structured Logging Pattern

```csharp
// Using ILogger (e.g., Serilog with Application Insights sink)
logger.LogInformation(
    "Client created: {@ClientCreatedEvent}",
    new {
        ClientId = clientId,
        ClientName = clientName,
        CreatedAt = createdAt,
        CreatedBy = userId,
        // Trace context AUTOMATICALLY added by middleware:
        TraceId = Activity.Current?.Id,
        SpanId = Activity.Current?.SpanId,
        // DO NOT INCLUDE:
        // Email = clientEmail,        // ← PII, forbidden
        // PhoneNumber = phone,        // ← PII, forbidden
        // Payload = fullRequest       // ← Entire request, forbidden
    });
```

#### Log Enrichment (Automatic via Middleware)

Middleware enriches all logs with trace context before they reach Application Insights:

```csharp
app.Use(async (context, next) =>
{
    // Extract or generate trace context
    var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
    var spanId = Activity.Current?.SpanId.ToString();
    
    // Enrich all logs in this scope
    using (LogContext.PushProperty("TraceId", traceId))
    using (LogContext.PushProperty("SpanId", spanId))
    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next.Invoke();
    }
});
```

**LOG-003, LOG-006:** Every log entry automatically includes TraceId/SpanId; duplicate logging avoided by logging at the boundary responsible for handling the error.

### 7. Instrumentation Domains

Every domain below uses automatic + custom instrumentation:

#### ASP.NET Core Requests (Automatic)
- Span per request with method, route, status code
- Automatic dependencies: HTTP clients, SQL queries, Service Bus sends

#### HTTP Clients (Automatic)
- Outgoing HTTP calls to other services (e.g., CRM → Identity)
- Trace propagation via `traceparent` header
- Request/response headers logged (without sensitive values)

#### SQL Server (Automatic + Custom)
- Automatic: SqlClient instrumentation captures queries, parameters, duration
- Custom: Business operation spans (e.g., `Client.Create`) wrap data-layer calls
- **OTEL-003:** Parameterized queries only (prevent SQL injection, protect PII)

```csharp
// ✓ Correct: Parameterized, safe
using var command = connection.CreateCommand();
command.CommandText = "SELECT * FROM Clients WHERE Id = @id";
command.Parameters.AddWithValue("@id", clientId);

// ✗ Wrong: String concatenation, SQL injection risk
command.CommandText = $"SELECT * FROM Clients WHERE Id = {clientId}";
```

#### Azure Service Bus (Automatic + Custom)
- Automatic: ServiceBusClient instrumentation captures publish/consume operations
- Message properties include `traceparent` header (propagation)
- Custom: Relay operations include correlation metadata in message headers
- Span includes message ID, operation (send/receive), status

#### Azure Functions (Automatic + Custom)
- Automatic: Function invocation captured as span
- Trigger type, invocation ID, success/failure
- Custom: Business operation spans inside Function (e.g., `Audit.Process` inside Audit.Functions)
- Timer Functions include lease/batch metrics

#### Business Operations (Custom Spans — OTEL-004)

When automatic instrumentation alone doesn't explain behavior, add business spans:

```csharp
// Example: Client lifecycle state transition
using var activity = new Activity("Client.UpdateLifecycle").Start();
activity.SetAttribute("old_status", oldStatus);
activity.SetAttribute("new_status", newStatus);
activity.SetAttribute("reason", reason);
activity.SetAttribute("client.id", clientId);

// ... authorization checks, validation, event emission ...
```

**Recommended spans:**
- Client.Create, Client.UpdateLifecycle, Client.Archive
- Project.Create, Project.UpdateStatus, Project.Complete
- Task.Create, Task.Assign, Task.Complete
- Outbox.Publish (relay operation)
- Audit.Append (audit consumer)

### 8. Business-Safe Identifiers

**TRACE-006:** Business identifiers may be logged; sensitive data must not be.

**Safe to log:**
```csharp
activity.SetAttribute("client.id", Guid);          // UUID, safe
activity.SetAttribute("project.id", Guid);         // UUID, safe
activity.SetAttribute("task.id", Guid);            // UUID, safe
activity.SetAttribute("user.id", userId);          // Authenticated user ID, safe
activity.SetAttribute("correlation.id", string);   // Internal tracking, safe
activity.SetAttribute("trace.id", W3CTraceId);     // Trace identifier, safe
```

**Forbidden to log:**
```csharp
// ✗ PII
activity.SetAttribute("client.email", email);
activity.SetAttribute("client.phone", phone);
activity.SetAttribute("client.first_name", firstName);

// ✗ Credentials
activity.SetAttribute("password", password);
activity.SetAttribute("auth_token", token);
activity.SetAttribute("api_key", apiKey);

// ✗ Full payloads
activity.SetAttribute("request_body", jsonPayload);
activity.SetAttribute("response_body", jsonPayload);
activity.SetAttribute("full_customer_record", customerObject);

// ✓ Sanitized alternatives
activity.SetAttribute("client.email_verified", isEmailVerified);  // Boolean, not address
activity.SetAttribute("client.country", country);                 // Metadata, not PII
activity.SetAttribute("operation_type", "CreateClient");          // Event type
activity.SetAttribute("payload_size_bytes", payloadSize);         // Size metric
```

### 9. Sampling and Retention

**Sampling ownership:** Azure Monitor team sets sampling rates for production; developers do not hard-code sampling logic.

#### Development (Local Aspire)
- **Sampling:** 100% (all traces exported to Aspire Dashboard)
- **Retention:** In-memory; cleared on application restart
- **Backend:** Aspire Dashboard UI

#### Production (Azure Monitor)
- **Sampling:** Configured in Application Insights resource settings
  - Default: 100% for first N traces/day, adaptive sampling after
  - Configurable per service/operation via sampling rules
- **Retention:** 90 days (adjustable)
- **Alert Thresholds:** Configurable without code changes

**Example Alert:**
```
If error_rate > 5% for 5 minutes → notify on-call
If request_duration_p99 > 2000ms for 10 minutes → notify SRE
If ServiceBus.DeadLetterCount > 0 → immediate escalation
```

### 10. Health Checks and Readiness

**OPS-001..002:** Operators determine service health and readiness.

#### Process Health
- Application is running and responding to requests
- Exposed via `/health` endpoint (liveness probe)
- Status: Healthy | Degraded | Unhealthy

#### Dependency Health
- SQL database connectivity
- Service Bus connectivity
- Downstream service availability (e.g., CRM → Identity HTTP call)
- Exposed via `/health/live` or health check details

#### Readiness
- Application is ready to accept traffic
- All dependencies initialized and healthy
- Exposed via `/health/ready` endpoint (Kubernetes readiness probe)

**Implementation:**
```csharp
// In Startup
services.AddHealthChecks()
    .AddSqlServer(
        connectionString,
        name: "sql-crm",
        tags: new[] { "db" })
    .AddAzureServiceBus(
        connectionString,
        name: "service-bus",
        tags: new[] { "messaging" })
    .AddCheck<DownstreamServiceHealthCheck>(
        name: "identity-service",
        tags: new[] { "service" });

// Endpoint
app.MapHealthChecks(
    "/health",
    new HealthCheckOptions { Predicate = _ => true });

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions { Predicate = hc => hc.Tags.Contains("live") });
```

### 11. Local Development: Aspire Dashboard

**During local development (Aspire-orchestrated):**

- **Trace Viewer:** Aspire Dashboard shows real-time traces from all running services/Functions
- **Metrics:** Real-time metrics (request rate, latency, exceptions)
- **Logs:** Structured logs from all services in one pane
- **No Export Required:** Local Aspire uses in-memory exporters; no Azure subscription needed
- **Parity:** Trace structure and attributes identical to production (Application Insights)

**Example Workflow:**

1. Developer runs `dotnet run` (or IDE task) to start Aspire AppHost
2. Aspire orchestrates and instruments all services locally
3. Developer hits browser endpoint → Crm API → SQL → Outbox
4. Timer Function fires every 10s → publishes to Service Bus
5. Audit.Functions consumes → appends to SQL
6. Developer opens Aspire Dashboard → queries TraceId
7. Single trace visible with all operations (HTTP, SQL, Service Bus, Function)
8. Developer clicks spans to see attributes, exceptions, logs

**Aspire Configuration (Concept):**
```csharp
// In Aspire AppHost, instrumentation automatically applied
var otel = builder.AddOpenTelemetry();
otel.WithTracing(tracing =>
{
    tracing.AddAspNetCoreInstrumentation();
    tracing.AddSqlClientInstrumentation();
    tracing.AddAzureServiceBusInstrumentation();
    // ...
});
otel.WithMetrics(metrics => { ... });
otel.WithLogs(logs => { ... });

// Exporter: Aspire console/dashboard (not Application Insights)
```

### 12. End-to-End Trace Flow: Browser → SQL

**Verification of BEHAVIOR requirement:**

Operator traces: Browser → YARP → API → SQL → Outbox → Timer Function → Service Bus → Consumer Function → SQL

```
┌─ Browser: POST /api/clients (no trace header)
│
├─ YARP Gateway (Span: gateway-001)
│  ├─ Generate TraceId = 4bf92f3577b34da6a3ce929d0e0e4736
│  ├─ SpanId = 00f067aa0ba902b7
│  └─ traceparent: 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01
│
├─ CRM.Api Request (Span: crm-req-001, parent: gateway-001)
│  ├─ Route: POST /api/clients → CreateClientAction
│  ├─ User: userId = user-ABC123
│  ├─ Attributes:
│  │  ├─ http.method = POST
│  │  ├─ http.url = /api/clients
│  │  ├─ http.status_code = 201
│  │  └─ user.id = user-ABC123
│  │
│  └─ Business Span: Client.Create (Span: client-create-001)
│     ├─ Facade validation
│     ├─ Business rules (lifecycle logic)
│     ├─ client.id = 550e8400-e29b-41d4-a716-446655440000
│     │
│     └─ SQL Span: INSERT Client (Span: sql-insert-001)
│        ├─ Parameterized query
│        ├─ Duration: 45ms
│        └─ Database: projectchicago_crm
│
│        Data Layer Transaction:
│        ├─ INSERT Clients (successful)
│        ├─ INSERT OutboxMessages (
│        │  ├─ MessageId = msg-DEF456
│        │  ├─ CorrelationId = corr-ABC123  [from HTTP request]
│        │  ├─ CausationId = req-XYZ789      [request ID]
│        │  ├─ OriginalTraceId = 4bf92f35... [this trace]
│        │  ├─ EventType = ClientCreated
│        │  └─ EventPayload = { clientId, name, ... }
│        │  )
│        └─ COMMIT (both rows)
│
├─ HTTP Response to Browser (200 OK)
│
├─ [Asynchronous] Timer-Triggered CRM.Functions (Span: relay-001, NEW TraceId = 8b5a3c2e...)
│  ├─ Trigger: Timer every 10 seconds
│  ├─ Activity.Current.TraceId = 8b5a3c2e...
│  │
│  └─ Business Span: Outbox.Publish (Span: outbox-publish-001)
│     ├─ SELECT OutboxMessages (WHERE PublishedAt IS NULL)
│     ├─ For each message:
│     │  ├─ Restore CorrelationId, CausationId, OriginalTraceId from DB row
│     │  ├─ Activity.AddLink(new ActivityLink(OriginalTraceId))
│     │  │  └─ Links this relay span to original HTTP trace
│     │  │
│     │  └─ ServiceBusSender.PublishAsync() (Span: sb-send-001)
│     │     ├─ Message Headers:
│     │     │  ├─ traceparent: 00-8b5a3c2e...-{relaySpanId}-01
│     │     │  ├─ correlation-id: corr-ABC123 [PRESERVED]
│     │     │  ├─ causation-id: msg-DEF456
│     │     │  ├─ original-trace-id: 4bf92f3577b34da6a3ce929d0e0e4736 [PRESERVED]
│     │     │  └─ message-id: msg-DEF456
│     │     │
│     │     └─ ServiceBus publishes (at-least-once)
│     │
│     └─ UPDATE OutboxMessages SET PublishedAt = now
│
├─ Service Bus Broker → Audit.Functions (Span: audit-func-001, NEW TraceId = c7f9d4b1...)
│  ├─ Trigger: Service Bus message received
│  ├─ Extract message headers:
│  │  ├─ correlation-id = corr-ABC123
│  │  ├─ original-trace-id = 4bf92f3577b34da6a3ce929d0e0e4736
│  │  └─ message-id = msg-DEF456
│  │
│  ├─ Activity.AddLink(
│  │    new ActivityLink(
│  │      new ActivityContext(
│  │        originalTraceId: 4bf92f3577b34da6a3ce929d0e0e4736,
│  │        parentSpanId: default)))
│  │  └─ Links this invocation (TraceId: c7f9d4b1) back to original (4bf92f35)
│  │
│  └─ Business Span: Audit.Append (Span: audit-append-001)
│     ├─ Inbox Check: SELECT WHERE MessageId = msg-DEF456
│     ├─ New Audit Entry:
│     │  ├─ Entity = Client
│     │  ├─ Action = Created
│     │  ├─ ClientId = 550e8400-e29b-41d4-a716-446655440000
│     │  ├─ CorrelationId = corr-ABC123
│     │  ├─ OriginalTraceId = 4bf92f3577b34da6a3ce929d0e0e4736
│     │  └─ CreatedAt = now
│     │
│     └─ SQL Span: INSERT AuditEntry (Span: sql-audit-insert-001)
│        ├─ Parameterized INSERT
│        ├─ Duration: 12ms
│        └─ Database: projectchicago_audit
│
│        Transaction:
│        ├─ INSERT InboxMessage (MessageId, ProcessedAt)
│        ├─ INSERT AuditEntry (full audit record)
│        └─ COMMIT (both rows)
│
└─ Application Insights Query:

   Query: @traceId == "4bf92f3577b34da6a3ce929d0e0e4736"
   
   Results:
   ├─ Trace: 4bf92f3577b34da6a3ce929d0e0e4736 (HTTP request)
   │  ├─ Gateway span: 00f067aa0ba902b7
   │  ├─ Crm API span: crm-req-001
   │  ├─ Client.Create span: client-create-001
   │  └─ SQL Insert span: sql-insert-001
   │
   ├─ Linked Trace: 8b5a3c2e... (relay, linked via causation)
   │  ├─ Relay span: relay-001
   │  ├─ Outbox.Publish span: outbox-publish-001
   │  └─ ServiceBus.Send span: sb-send-001
   │
   ├─ Linked Trace: c7f9d4b1... (audit function, linked via original-trace-id)
   │  ├─ Audit.Functions span: audit-func-001
   │  ├─ Audit.Append span: audit-append-001
   │  └─ SQL Insert (audit) span: sql-audit-insert-001
   │
   └─ Logs (all with TraceId: 4bf92f35..., or linked via CorrelationId)
      ├─ Client created: {@Event} [TraceId: 4bf92f35...]
      ├─ Outbox message published [TraceId: 8b5a3c2e, OriginalTraceId: 4bf92f35]
      └─ Audit event appended [TraceId: c7f9d4b1, OriginalTraceId: 4bf92f35]

   Operator can also query:
   ├─ @correlationId == "corr-ABC123" → returns all events in business flow
   ├─ @clientId == "550e8400..." → returns all telemetry for this entity
   └─ @service == "crm" AND @error == true → all errors in CRM service
```

**✓ Verification Complete:** Operator can start with original HTTP TraceId and follow the entire flow through gateway, API, SQL, outbox, timer relay, Service Bus, consumer Function, and audit SQL through Application Insights queries and trace links.

---

## Consequences

### Positive

- **Complete Traceability** [TRACE-001..007]: Every request is traceable from browser through all services, databases, and async operations
- **Operational Visibility** [OBS-001..005]: Single pane of glass (Application Insights) for alerts, dashboards, diagnostics
- **Log-Trace Correlation** [LOG-003, LOG-006]: Logs automatically include trace context; no manual correlation needed
- **Business Intelligence** [TRACE-006]: Operators can filter by Client/Project/Task ID without exposing PII
- **Dependency Insight** [OTEL-003]: Automatic instrumentation reveals performance bottlenecks (slow SQL, slow HTTP, slow Service Bus)
- **Async Debugging** [TRACE-004]: Asynchronous work remains linked to originating request through correlation/causation chain
- **Local Development** [OBS-001]: Aspire Dashboard provides production-like observability parity locally
- **Health Awareness** [OPS-001..002]: Structured health checks distinguish process, dependency, and readiness

### Negative/Tradeoffs

- **Instrumentation Complexity**: Every service must properly propagate W3C trace context and correlation metadata; mistakes cause broken traces
- **Sampling Tuning**: Production sampling rates must be carefully tuned to balance cost (storage, ingestion quota) vs. observability (coverage)
- **Sensitive Data Risk**: Developers must be disciplined about NOT logging PII; one careless log inclusion can violate privacy regulations
- **Storage Cost**: Application Insights retention and ingestion volume will grow with request rate; cost monitoring required
- **External Dependency**: Production observability depends on Azure Monitor/Application Insights availability and quota

---

## Acceptance Criteria

This ADR is accepted when:

1. ✓ OpenTelemetry packages (`System.Diagnostics.DiagnosticSource`, `OpenTelemetry.*`) are added to all service projects
2. ✓ W3C Trace Context propagation is implemented:
   - [ ] YARP generates/preserves `traceparent` header
   - [ ] CRM/Identity APIs extract and propagate to dependencies
   - [ ] Outbox records preserve `CorrelationId`, `CausationId`, `OriginalTraceId`
   - [ ] Timer relay includes correlation metadata in Service Bus message headers
   - [ ] Audit.Functions extracts and links back to original trace
3. ✓ Application Insights exporter configured (production):
   - [ ] Connection string from Aspire/configuration
   - [ ] Sampling rate (configurable, not hardcoded)
   - [ ] Resource attributes set (service name, version, environment)
4. ✓ Aspire Dashboard configured (local development):
   - [ ] In-memory OTLP exporter for traces/metrics/logs
   - [ ] Dashboard accessible during `dotnet run`
5. ✓ Automatic instrumentation for:
   - [ ] ASP.NET Core requests (method, route, status, latency)
   - [ ] HTTP clients (outgoing calls with propagation)
   - [ ] SQL Server (queries, parameters, latency, dependencies)
   - [ ] Azure Service Bus (publish/consume with correlation headers)
   - [ ] Azure Functions (invocation, trigger type, status)
6. ✓ Business operation spans created for:
   - [ ] Client.Create, Client.UpdateLifecycle, Client.Archive
   - [ ] Project.Create, Project.UpdateStatus, Project.Complete
   - [ ] Task.Create, Task.Assign, Task.Complete
   - [ ] Outbox.Publish (relay operation)
   - [ ] Audit.Append (consumer processing)
7. ✓ Structured logging with automatic trace correlation:
   - [ ] Serilog (or equivalent) configured
   - [ ] Logs include structured properties (not free-text)
   - [ ] TraceId/SpanId automatically added via middleware
   - [ ] No customer payloads, passwords, or sensitive data
8. ✓ Health checks implemented:
   - [ ] `/health` (liveness: application running)
   - [ ] `/health/live` (dependency status)
   - [ ] `/health/ready` (readiness to serve)
9. ✓ No hardcoded sensitive data in telemetry:
   - [ ] Code review confirms no email/phone/full names in logs/traces
   - [ ] No raw request/response bodies logged
   - [ ] Parameterized SQL only (no string concatenation)
10. ✓ Test coverage:
    - [ ] Unit tests verify business operation spans created
    - [ ] Integration tests verify end-to-end trace continuity (HTTP → SQL → Outbox → Service Bus → Function → SQL)
    - [ ] Trace attributes verified for correctness and data safety
11. ✓ Documentation:
    - [ ] Developer guide: How to add business spans, attach business identifiers, propagate trace context
    - [ ] Operator guide: How to query Application Insights by TraceId, correlate logs, investigate failures
    - [ ] Sampling policy documented (rates, retention, cost model)

---

## Links and References

- **ADR-0015**: Bounded-context catalog (six services, distributed)
- **ADR-0016**: Audit architecture (event-driven, requires trace linkage)
- **ADR-0017**: Service Bus topology (requires trace propagation in message headers)
- **CLAUDE.md § Observability**: High-level guidance
- **.claude/rules/** (future): Detailed instrumentation rules by layer
- **OTEL-001..006**: OpenTelemetry requirements
- **TRACE-001..007**: Distributed trace requirements
- **OBS-001..005**: Observability backend and dashboard requirements
- **LOG-001..006**: Structured logging requirements
- **OPS-001..004**: Operational observability and health requirements
- **W3C Trace Context**: https://www.w3.org/TR/trace-context/
- **OpenTelemetry .NET**: https://opentelemetry.io/docs/instrumentation/net/
- **Azure Application Insights**: https://learn.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview

This ADR establishes the observability foundation for Project Chicago. Implementation follows in individual service projects, guided by the developer and operator guides created during acceptance.
