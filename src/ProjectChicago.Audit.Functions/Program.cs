using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using ProjectChicago.Audit.Core.Persistence;

var builder = FunctionsApplication.CreateBuilder(args);

// ServiceDefaults: service discovery, HTTP client resilience, and the shared OpenTelemetry pipeline
// (SQL Server + Azure.* instrumentation, resource attributes, OTLP/Azure Monitor exporters).
builder.AddServiceDefaults();

// Functions-specific OpenTelemetry correlation: aligns the Functions host's own telemetry with
// worker-emitted spans so a single invocation doesn't produce duplicate/disjoint traces (ASYNC-003).
builder.Services.AddOpenTelemetry().UseFunctionsWorkerDefaults();

// Aspire SQL Server EF Core client integration: connection string, resilience, health check, and
// telemetry come from the "AuditDb" connection name injected by AppHost.
builder.AddSqlServerDbContext<AuditDbContext>("AuditDb");

// Aspire Azure Service Bus client integration: Audit Functions consumes from Service Bus
// subscriptions (ADR-0016, ADR-0017).
builder.AddAzureServiceBusClient("messaging");

var host = builder.Build();

host.RunAsync().GetAwaiter().GetResult();
