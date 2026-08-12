var builder = DistributedApplication.CreateBuilder(args);

// One local SQL Server container resource; each bounded service gets its own database resource
// added to this later (DATA-030/031). No password/port specified - Aspire generates and manages the
// administrator credential as a secret parameter rather than a hardcoded value (DEPLOY-001).
var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent);

// Local Azure Service Bus emulator, topology per ADR-0017 (confirmed in CLAUDE.md): a single shared
// topic "ProjectChicago.Events" with one subscription per consuming service - "Audit" initially,
// with Notification/Search/Workflow subscriptions added later without changing this topology.
// MaxDeliveryCount is left at the Aspire default of 10, which already matches the ADR's bounded-retry
// value for ASYNC-007. No project references this resource yet.
var messaging = builder.AddAzureServiceBus("messaging")
    .RunAsEmulator();

var eventsTopic = messaging.AddServiceBusTopic("events-topic", "ProjectChicago.Events");

eventsTopic.AddServiceBusSubscription("audit-subscription", "Audit");

// The gateway is Project Chicago's only browser-facing HTTP edge (SEC-020, gateway.md). Registered
// as a plain project resource for Aspire service discovery/health/telemetry defaults; no routes to
// backend service API hosts are wired here since none exist yet, and it gets no SQL/Service Bus
// reference - the gateway never talks to either directly.
builder.AddProject<Projects.ProjectChicago_Gateway>("gateway");

// The React/Vite client (frontend.md). runScriptName is explicit even though "dev" is already
// AddViteApp's default, to keep it visibly tied to package.json's "dev": "vite" script rather than
// an implicit default. Package manager is npm (the repo's package-lock.json), which is also
// AddViteApp's default with no WithBun/WithYarn/WithPnpm call. No API base URL, route, auth, or
// other service reference is wired yet - just the resource itself.
builder.AddViteApp("web", "../web", "dev");

builder.Build().Run();
