using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using ProjectChicago.Identity.Core.Persistence;
using ProjectChicago.Identity.Functions.Outbox;
using ProjectChicago.Shared.Messaging;

var builder = FunctionsApplication.CreateBuilder(args);

// No HTTP triggers exist or are allowed here (functions.md: YARP + service HTTP hosts are the only
// HTTP application edge), so ConfigureFunctionsWebApplication() is intentionally not called;
// FunctionsApplication.CreateBuilder() already applies the isolated-worker defaults on its own.

// ServiceDefaults: service discovery, HTTP client resilience, and the shared OpenTelemetry pipeline
// (SQL Server + Azure.* instrumentation, resource attributes, OTLP/Azure Monitor exporters) used by
// every composition root in the solution (ADR-0021, aspire.md).
builder.AddServiceDefaults();

// Functions-specific OpenTelemetry correlation: aligns the Functions host's own telemetry with
// worker-emitted spans so a single invocation doesn't produce duplicate/disjoint traces (ASYNC-003).
builder.Services.AddOpenTelemetry().UseFunctionsWorkerDefaults();

// Aspire SQL Server EF Core client integration: connection string, resilience, health check, and
// telemetry come from the "IdentityDb" connection name injected by AppHost (aspire.md/database.md) -
// same integration as the Identity HTTP host. IdentityDbContext never calls UseSqlServer directly.
builder.AddSqlServerDbContext<IdentityDbContext>("IdentityDb");

// Aspire Azure Service Bus client integration: connection/credential come from the "messaging"
// connection name injected by AppHost (ADR-0017). This service's Function app is the Identity publisher
// side of the outbox pattern (OUTBOX-003) and is the only Identity composition root approved for Service
// Bus credentials (aspire.md: the Identity HTTP host does not receive this reference). No relay/trigger
// logic is added in this step - only the client is made available to DI.
builder.AddAzureServiceBusClient("messaging");

// RelayOutboxFunction's schedule/entity-name/batch/lease settings, bound from the "Identity:OutboxRelay"
// configuration section (messaging.md: schedule/batch/lease are operational configuration, never
// hardcoded).
builder.Services.Configure<OutboxRelaySettings>(builder.Configuration.GetSection("Identity:OutboxRelay"));

// Identity's own SQL Server-backed outbox store (OUTBOX-003..006) - scoped to match IdentityDbContext's own
// (Aspire-registered) scoped lifetime, since it claims/settles rows through that context.
builder.Services.AddScoped<IOutboxStore, IdentityOutboxStore>();

// Shared Service Bus publisher (ProjectChicago.Shared). Singleton to match the ServiceBusClient
// Aspire registers above and to reuse this publisher's per-entity ServiceBusSender cache across
// relay runs instead of recreating senders every invocation.
builder.Services.AddSingleton<IServiceBusPublisher, AzureServiceBusPublisher>();

// Shared outbox relay (ProjectChicago.Shared): claims a batch via IOutboxStore, publishes each
// message via IServiceBusPublisher, and settles it - the only thing RelayOutboxFunction delegates to
// (functions.md). Scoped because it depends on the scoped IOutboxStore/IdentityDbContext.
builder.Services.AddScoped<IOutboxRelay, OutboxRelay>();

var host = builder.Build();

host.Run();
