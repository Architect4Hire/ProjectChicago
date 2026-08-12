# ADR-0011 — OpenTelemetry with Azure Monitor/Application Insights

- **Status:** Accepted
- **Requirements:** OTEL-001..006, OBS-001..005, LOG-001..006, OPS-001..004

## Context
Project Chicago explicitly requires every API/service/Function to support OpenTelemetry and a centralized single pane of glass.

## Decision
OpenTelemetry is the standard instrumentation mechanism for traces, metrics and log correlation. Aspire ServiceDefaults provides the common baseline. Azure Monitor/Application Insights is the primary production operational view; Aspire Dashboard supports local development.

Instrument ASP.NET Core, outgoing HTTP, SQL/EF, Azure Service Bus and Azure Functions. Add custom business spans only where automatic instrumentation cannot explain a meaningful business operation.

## Consequences
- Resource attributes consistently identify service, version and environment.
- Sensitive payloads, secrets and SQL parameter values are excluded.
- Metrics use low-cardinality dimensions.
- Structured logs correlate to current trace context.
- Sampling/export configuration is centrally controlled.

## Validation
Telemetry tests and the end-to-end Client trace prove browser-edge through Function/Service Bus participation; operational dashboards cover rate/error/latency/dependencies/outbox/DLQ.
