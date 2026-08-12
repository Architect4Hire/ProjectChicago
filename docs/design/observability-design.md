# Project Chicago — Observability Design

## Goal

An operator must be able to start with a user-visible support reference or TraceId and reconstruct an operation from the edge to its final synchronous and asynchronous effects.

## Observability stack

- OpenTelemetry instrumentation standard.
- Aspire ServiceDefaults for common registration.
- Aspire Dashboard for local traces/logs/health.
- Azure Monitor/Application Insights for production single pane of glass.
- Structured logs correlated with current trace.
- Metrics for operational health and durable-processing backlogs.

## Canonical trace

```text
React
 → YARP
 → CRM API
 → Facade
 → Business
 → Data transaction
 → SQL
 → Outbox
 → Timer Function
 → Azure Service Bus
 → Audit ServiceBusTrigger Function
 → Audit Core
 → Audit SQL
```

## Identity model for diagnostics

| Identifier | Purpose |
|---|---|
| TraceId | Distributed technical trace |
| SpanId | Current technical operation |
| CorrelationId | Stable logical/business flow correlation |
| CausationId | Immediate causal operation/event |
| Event/MessageId | Durable message identity + idempotency |
| Entity ID | Optional safe business lookup (Client/Project/Task) |

Trace parentage may change across delayed async work, but correlation/causation remains explicit.

## Automatic instrumentation

Enable where supported:
- ASP.NET Core requests,
- outgoing HTTP,
- EF Core/SQL client,
- Azure Service Bus,
- Azure Functions,
- runtime/process telemetry supplied by standard integrations.

Do not capture SQL parameters or message bodies indiscriminately.

## Custom spans

Use stable operation names for important business behavior, for example:
- `Client.Create`
- `Client.UpdateLifecycle`
- `Project.Create`
- `Project.ChangeStatus`
- `Task.Assign`
- `Task.ChangeStatus`
- `Outbox.Publish`

Avoid a span per method. The trace should explain the business operation, not mirror the call stack.

## Structured logging

Log properties rather than parse-dependent message strings. Typical safe context:
- service,
- environment,
- version,
- TraceId/CorrelationId,
- operation,
- route/method/status,
- safe entity ID,
- exception type.

Do not log entire request/response payloads by default.

Exceptions should normally be recorded at the boundary that handles/reports them rather than duplicated at every layer.

## Metrics

Required operational views include:
- request rate/error/latency,
- dependency latency/failure,
- Function executions,
- Service Bus consumer failures,
- dead-letter accumulation,
- SQL health,
- outbox pending count,
- oldest pending outbox age,
- outbox publication failures/retries,
- Audit consumer outcomes.

Metric labels must remain low-cardinality; do not label metrics by ClientId/ProjectId/TaskId.

## Single pane of glass

The production workbook/dashboard should answer:

1. Is the system healthy?
2. Which service/Function is failing?
3. Which dependency is slow?
4. Are outbox messages accumulating?
5. Are Service Bus messages dead-lettering?
6. Can I paste a TraceId/CorrelationId and see the whole flow?
7. Did the associated audit event persist?

## Sampling

Sampling is centrally configured and must not make critical operational/audit correlation impossible. Audit persistence does not depend on telemetry sampling.

## Verification

The canonical proof is one Client creation where:
- the same logical correlation is visible at every hop,
- the original HTTP trace is discoverable,
- SQL/outbox/Function/Service Bus spans are visible or linked,
- one AuditEntry exists,
- a duplicate message does not create another AuditEntry.
