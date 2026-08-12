# OpenTelemetry Observability

Use one telemetry vocabulary across HTTP hosts and Functions.

## Common resource attributes
- service.name,
- service.version,
- deployment.environment.

## Standard instrumentation
ASP.NET Core, outgoing HTTP, SQL/EF, Azure Service Bus and Functions.

## Business spans
Add only where they explain a user/business operation unavailable from auto-instrumentation.

## Logs
Structured, trace-correlated, safe. Do not dump payloads.

## Metrics
Low-cardinality operational signals. Entity IDs belong in traces/log search when necessary, not metric labels.

## Export
Local: Aspire Dashboard. Production: Azure Monitor/Application Insights via OpenTelemetry exporter/configuration.

## Proof
The best observability test is a trace you can follow, not merely DI registration that compiles.
