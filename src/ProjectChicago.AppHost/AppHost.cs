
var builder = DistributedApplication.CreateBuilder(args);

// One local SQL Server container resource; each bounded service gets its own database resource
// added to this later (DATA-030/031). No password/port specified - Aspire generates and manages the
// administrator credential as a secret parameter rather than a hardcoded value (DEPLOY-001).
var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent);

// CRM's own SQL database (ADR-0015: "CrmDb", DATA-031..034). Resource name doubles as the
// Aspire-injected connection name the Crm host/Functions composition roots resolve via the SQL
// Server EF Core integration (DEPLOY-001).
var crmDb = sql.AddDatabase("CrmDb");

// Identity's own SQL database (ADR-0015: "IdentityDb", DATA-031..034, SEC-001). Resource name
// doubles as the Aspire-injected connection name the Identity host/Functions composition roots
// resolve via the SQL Server EF Core integration (DEPLOY-001). IdentityDbContext includes
// ASP.NET Core Identity tables plus Outbox/Inbox for event-driven mutations (ASYNC-005).
var identityDb = sql.AddDatabase("IdentityDb");

// Audit's own SQL database (ADR-0016: "AuditDb", DATA-031..034). Resource name doubles as the
// Aspire-injected connection name the Audit host/Functions composition roots resolve via the SQL
// Server EF Core integration (DEPLOY-001). AuditDbContext includes append-only audit entries and
// inbox for idempotent Service Bus-triggered consumption (AUDIT-001..008, ASYNC-005).
var auditDb = sql.AddDatabase("AuditDb");

// Local Azure Service Bus emulator, topology per ADR-0017 (confirmed in CLAUDE.md): a single shared
// topic "ProjectChicago.Events" with one subscription per consuming service - "Audit" initially,
// with Notification/Search/Workflow subscriptions added later without changing this topology.
// MaxDeliveryCount is left at the Aspire default of 10, which already matches the ADR's bounded-retry
// value for ASYNC-007.
var messaging = builder.AddAzureServiceBus("messaging")
    .RunAsEmulator();

var eventsTopic = messaging.AddServiceBusTopic("events-topic", "ProjectChicago.Events");

eventsTopic.AddServiceBusSubscription("audit-subscription", "Audit");

// Redis cache resource (ADR-0018-superseding BFF design): holds the server-side session record
// {accessToken, refreshToken, userId, expiries} keyed by opaque session ID. Persistent lifetime keeps
// local sessions alive across AppHost restarts during development, matching the "sql" resource's
// convention. Gateway is the only consumer (WaitFor ensures Redis is ready before Gateway starts).
var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);

// Shared JWT signing secret (HMAC-SHA256, ADR-0018-superseding decision: symmetric key, no MFA/
// asymmetric complexity needed). Fixed 32-byte key for HS256 (requires minimum 128 bits / 16 bytes).
// Only Identity (signs) and the JWT-validating services (CRM, Audit, Identity itself for its own
// [Authorize] endpoints) receive it; Gateway never sees it (Gateway treats tokens as opaque).
const string jwtSigningKey = "ThisIsA32ByteKeyFor256BitHMACWithSHA256Algorithm";

// CRM bounded-service HTTP host (ADR-0015). Composition-only: ServiceDefaults plus the Aspire SQL
// Server EF Core integration for its own "CrmDb" database (DATA-030..034). WaitFor ensures the SQL
// container is ready before Crm starts; no messaging reference here since the host has no Service
// Bus wiring yet (aspire.md: don't give an API host Service Bus credentials it doesn't use).
var crm = builder.AddProject<Projects.ProjectChicago_Crm>("crm")
    .WithReference(crmDb)
    .WaitFor(crmDb)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey);

// CRM's sibling Azure Functions project (ADR-0015), the only asynchronous entry point for this
// service (functions.md). It is the CRM publisher side of the outbox pattern (OUTBOX-003), so -
// unlike the "crm" HTTP host above - it receives both the "CrmDb" reference (to read/mark outbox
// rows) and the shared Service Bus resource (to relay them). No triggers exist yet; this step only
// proves least-privilege composition wiring. Implicit Aspire-managed host storage is used (no
// explicit WithHostStorage) since no storage-specific behavior is required yet.
builder.AddAzureFunctionsProject<Projects.ProjectChicago_Crm_Functions>("crm-functions")
    .WithReference(crmDb)
    .WaitFor(crmDb)
    .WithReference(messaging)
    .WaitFor(messaging);

// Identity bounded-service HTTP host (ADR-0015). Composition-only: ServiceDefaults plus the Aspire
// SQL Server EF Core integration for its own "IdentityDb" database (DATA-031..034, SEC-001..004).
// WaitFor ensures the SQL container is ready before Identity starts. Does not receive Service Bus
// reference since the HTTP host has no direct messaging wiring (aspire.md: don't give an API host
// Service Bus credentials it doesn't use); only Identity.Functions publishes Identity events.
var identity = builder.AddProject<Projects.ProjectChicago_Identity>("identity")
    .WithReference(identityDb)
    .WaitFor(identityDb)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey);

// Identity's sibling Azure Functions project (ADR-0015), the only asynchronous entry point for
// this service (functions.md). It is the Identity publisher side of the outbox pattern (OUTBOX-003),
// so - unlike the "identity" HTTP host above - it receives both the "IdentityDb" reference (to
// read/mark outbox rows) and the shared Service Bus resource (to relay authentication/account
// events). No triggers exist yet; this step only proves least-privilege composition wiring.
builder.AddAzureFunctionsProject<Projects.ProjectChicago_Identity_Functions>("identity-functions")
    .WithReference(identityDb)
    .WaitFor(identityDb)
    .WithReference(messaging)
    .WaitFor(messaging);

// Audit bounded-service HTTP host (ADR-0015, ADR-0016). Composition-only: ServiceDefaults plus the
// Aspire SQL Server EF Core integration for its own "AuditDb" database (DATA-030..034, AUDIT-001..008).
// WaitFor ensures the SQL container is ready before Audit starts. Does not receive Service Bus reference
// since the HTTP host has no direct messaging wiring (aspire.md: don't give an API host Service Bus
// credentials it doesn't use); only Audit.Functions consumes events.
var audit = builder.AddProject<Projects.ProjectChicago_Audit>("audit")
    .WithReference(auditDb)
    .WaitFor(auditDb)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey);

// Audit's sibling Azure Functions project (ADR-0015, ADR-0016), the only asynchronous entry point
// for this service (functions.md). It is the Audit consumer side, receiving Service Bus-triggered
// integration events from CRM/Identity and persisting append-only audit entries idempotently using
// the inbox pattern (AUDIT-001..008, ASYNC-005..007). Receives both "AuditDb" reference (for inbox/audit
// entry persistence) and the shared Service Bus resource (to consume events). Least-privilege: Audit
// Functions only needs receive permissions on the "Audit" subscription (ADR-0017, messaging.md).
builder.AddAzureFunctionsProject<Projects.ProjectChicago_Audit_Functions>("audit-functions")
    .WithReference(auditDb)
    .WaitFor(auditDb)
    .WithReference(messaging)
    .WaitFor(messaging);

// The gateway is Project Chicago's only browser-facing HTTP edge (SEC-020, gateway.md). It routes
// stable public paths to owning service API hosts via service discovery. Route configuration is
// declared in gateway appsettings.json; AppHost wires references here (aspire.md: least-privilege).
// Identity service is wired for /auth/* route (authentication/account endpoints, SEC-020, API-001..007).
// Audit service is wired for /api/audit/* route (read-only audit entry queries, AUDIT-001..008, SEC-012).
var gateway = builder.AddProject<Projects.ProjectChicago_Gateway>("gateway")
    .WithReference(identity)  // Identity host needed for /auth/* routing
    .WithReference(crm)
    .WithReference(audit)     // Audit host needed for /api/audit/* routing
    .WithReference(redis)
    .WaitFor(redis);          // Redis cache for session storage (ADR-0018-superseding)

// The React/Vite client (frontend.md). runScriptName is explicit even though "dev" is already
// AddViteApp's default, to keep it visibly tied to package.json's "dev": "vite" script rather than
// an implicit default. Package manager is npm (the repo's package-lock.json), which is also
// AddViteApp's default with no WithBun/WithYarn/WithPnpm call. Per aspire.md ("React app: reference/
// expose only the gateway address/config"), it receives only the gateway's resolved HTTPS endpoint -
// no service database, Service Bus, or internal service endpoint - injected as VITE_API_BASE_URL,
// which src/web/src/api/http.ts reads via import.meta.env to build the single typed gateway client
// base URL (frontend.md: "Browser talks to exactly one backend origin/base URL: YARP gateway").
builder.AddViteApp("web", "../web", "dev")
    .WithHttpsEndpoint(port: 5173)
    .WithReference(gateway)
    .WaitFor(gateway)
    .WithEnvironment("VITE_API_BASE_URL", gateway.GetEndpoint("https"));

// Build the application and run it. Database migrations are applied by each service's
// composition root in their Program.cs using Aspire's EF Core integration (DATA-034).
// Each bounded service applies its own migrations independently via the AddSqlServerDbContext
// Aspire helper which includes migration support (DATA-031: one database per service).
// See: https://learn.microsoft.com/en-us/dotnet/aspire/database/sql-server-component?tabs=dotnet-cli
var app = builder.Build();
await app.RunAsync();
