using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Contracts.Projects;
using ProjectChicago.Crm.Contracts.Tasks;
using ProjectChicago.Crm.Core.Facades;
using ProjectChicago.Crm.Core.Persistence;
using ProjectChicago.Crm.Core.Repositories;
using ProjectChicago.ServiceDefaults.Correlation;
using ProjectChicago.ServiceDefaults.Errors;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Business;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Aspire SQL Server EF Core client integration: connection string, resilience, health check, and
// telemetry come from the "CrmDb" connection name injected by AppHost (aspire.md/database.md).
// CrmDbContext itself never calls UseSqlServer or holds a connection string.
builder.AddSqlServerDbContext<CrmDbContext>("CrmDb");

// ERROR-001..005/TRACE-001..007: shared Problem Details/exception handling and the HTTP
// request/actor context adapter are host-owned composition (backend.md), not part of
// AddServiceDefaults - the sibling Functions project has no HttpContext to adapt.
builder.Services.AddHttpRequestContext();
builder.Services.AddApiExceptionHandling();

// JWT bearer authentication (ADR-0018-superseding, BFF pattern: validates JWT tokens injected by the Gateway).
// The Gateway holds JWT tokens server-side in Redis and injects Authorization: Bearer headers on proxied requests.
// CRM never receives the signing key (it stays with Identity); CRM validates using the shared public Issuer/Audience.
// Tokens are issued by Identity, validated by ASP.NET Core's JWT handler reading Jwt config (Issuer, Audience, SigningKey from env).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var config = builder.Configuration.GetSection("Jwt");
        var issuer = config["Issuer"];
        var audience = config["Audience"];
        var signingKey = Environment.GetEnvironmentVariable("Jwt__SigningKey");

        if (string.IsNullOrWhiteSpace(issuer))
            throw new InvalidOperationException("JWT Issuer is not configured");
        if (string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("JWT Audience is not configured");
        if (string.IsNullOrWhiteSpace(signingKey))
            throw new InvalidOperationException("JWT SigningKey is not configured via environment variable Jwt__SigningKey");

        options.TokenValidationParameters.ValidAudience = audience;
        options.TokenValidationParameters.ValidIssuer = issuer;
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.IssuerSigningKey =
            new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(signingKey));
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.ClockSkew = TimeSpan.Zero;

        // Short-lived access tokens don't need a refresh challenge; let the response propagate as 401.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";
                var problem = new { type = "https://tools.ietf.org/html/rfc7231#section-6.3.1", title = "Unauthorized", status = 401, traceId = context.HttpContext.TraceIdentifier };
                context.Response.WriteAsJsonAsync(problem);
                context.HandleResponse();
                return Task.CompletedTask;
            }
        };
    });

// SEC-010..016: Define authorization policies for CRM roles (Administrator, Manager, Contributor,
// ReadOnly). Each role maps to CRM capabilities using least-privilege principle (SEC-016).
// Policies are evaluated by ASP.NET Core's authorization middleware before controller actions;
// resource-level authorization remains in Facade/IClientAuthorization/IProjectAuthorization/
// ITaskAuthorization (SEC-010: authorization enforcement on the server; identity.md: "The service
// that owns a resource/action evaluates authorization against trusted authenticated context").
builder.Services.AddAuthorization(options =>
{
    // Administrator: full read and write access to all CRM resources (Clients, Projects, Tasks).
    options.AddPolicy("CRM.Administrator",
        policy => policy.RequireRole("Administrator"));

    // Manager: full read and write access to Clients, Projects, and Tasks (same as Administrator
    // for now; role separation allows future refinement of Manager capabilities).
    options.AddPolicy("CRM.Manager",
        policy => policy.RequireRole("Administrator", "Manager"));

    // Contributor: read access to Clients and Projects; full access to Tasks (create, assign,
    // update, complete). Supports collaborative workflows where contributors work on assigned
    // tasks but do not manage Clients or Projects (SEC-012/013: every operation has explicit
    // authorization).
    options.AddPolicy("CRM.Contributor",
        policy => policy.RequireRole("Administrator", "Manager", "Contributor"));

    // ReadOnly: view-only access to all CRM resources. No mutations permitted (SEC-012/013).
    options.AddPolicy("CRM.ReadOnly",
        policy => policy.RequireRole("Administrator", "Manager", "Contributor", "ReadOnly"));

    // ClientsApiContract policies (SEC-010..013): Clients.Read for list/detail queries,
    // Clients.Write for create/update/archive/restore mutations. Mapped to role-based policies:
    // Clients.Read = CRM.Contributor (or higher), Clients.Write = CRM.Manager (or higher).
    options.AddPolicy(ClientsApiContract.RequiredReadAuthorizationPolicy,
        policy => policy.RequireRole("Administrator", "Manager", "Contributor"));

    options.AddPolicy(ClientsApiContract.RequiredAuthorizationPolicy,
        policy => policy.RequireRole("Administrator", "Manager"));

    // ProjectsApiContract policies (SEC-010..013): Projects.Read for list queries, Projects.Write
    // for create/update mutations. Mapped to role-based policies: Projects.Read = CRM.Contributor
    // (or higher), Projects.Write = CRM.Manager (or higher).
    options.AddPolicy("Projects.Read",
        policy => policy.RequireRole("Administrator", "Manager", "Contributor"));

    options.AddPolicy("Projects.Write",
        policy => policy.RequireRole("Administrator", "Manager"));

    // TasksApiContract policies (SEC-010..013): Tasks.Read for list queries, Tasks.Write for
    // create/assign/update/complete mutations. Mapped to role-based policies: Tasks.Read =
    // CRM.Contributor (or higher), Tasks.Write = CRM.Contributor (or higher) - Contributors can
    // work on their assigned tasks.
    options.AddPolicy("Tasks.Read",
        policy => policy.RequireRole("Administrator", "Manager", "Contributor"));

    options.AddPolicy("Tasks.Write",
        policy => policy.RequireRole("Administrator", "Manager", "Contributor"));
});

// Domain onion composition (onion-boundaries.md): Data/Repository, Business, Facade layers
// for each bounded service's use cases. Controllers reference only Facade interfaces, which
// reference only Business interfaces, which reference only Data interfaces.
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IClientData, ClientData>();
builder.Services.AddScoped<IClientBusiness, ClientBusiness>();
builder.Services.AddScoped<IClientAuthorization, ClientAuthorization>();
builder.Services.AddScoped<IClientFacade, ClientFacade>();

builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IProjectData, ProjectData>();
builder.Services.AddScoped<IProjectBusiness, ProjectBusiness>();
builder.Services.AddScoped<IProjectAuthorization, ProjectAuthorization>();
builder.Services.AddScoped<IProjectFacade, ProjectFacade>();

builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<ITaskData, TaskData>();
builder.Services.AddScoped<ITaskBusiness, TaskBusiness>();
builder.Services.AddScoped<ITaskAuthorization, TaskAuthorization>();
builder.Services.AddScoped<ITaskFacade, TaskFacade>();

// Mechanism-neutral abstractions (onion-boundaries.md: "Facades depend only on ... abstractions
// such as current user, clock, cache, and correlation context"). The Clock abstraction lets
// Facades resolve "now" in a testable way without coupling to DateTime.UtcNow directly.
builder.Services.AddSingleton<IClock, Clock>();

// CORS configuration (ADR-0018, SEC-020: allow React client on localhost for development).
// In production, restrict origins and carefully control credentials policy.
// AllowAnyOrigin() and AllowCredentials() cannot both be true; use specific origins in production.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", corsBuilder =>
    {
        corsBuilder
            .SetIsOriginAllowed(origin => origin.Contains("localhost") || origin.Contains("127.0.0.1"))
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// MVC controllers are the only HTTP application edge (onion-boundaries.md: "Use ASP.NET Core MVC
// controllers ...; do not add minimal API routes"). AddOpenApi/MapOpenApi satisfies API-006 - every
// public API contract is documented through OpenAPI, keyed by each action's stable OperationId
// (e.g. ClientsApiContract.CreateOperationId).
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply pending EF Core migrations (DATA-034: migrations through controlled deployment).
// Runs synchronously on startup to ensure schema is ready before the service serves requests.
// If migration fails, service startup fails (correct behavior for schema corruption/errors).
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
    await dbContext.Database.MigrateAsync();
    Console.WriteLine("[CRM] ✓ Database migrations applied successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"[CRM] ✗ Database migration failed: {ex.Message}");
    throw;
}

app.UseExceptionHandler();

// Keeps status-code-only failures (no exception - e.g. an unmatched route today, or a future
// [ApiController] automatic 400) in the same ApiProblemDetailsCustomizer shape as exception-driven
// failures, instead of a bare empty-body status code (ERROR-001).
app.UseStatusCodePages();

app.MapDefaultEndpoints();

// Forwarded headers middleware (SEC-020, gateway.md): Trust X-Forwarded-* headers from YARP gateway.
// Required for services behind reverse proxy to recognize the correct protocol (X-Forwarded-Proto: http)
// and not redirect HTTP → HTTPS unnecessarily when receiving gateway traffic.
// In production, configure to trust only the gateway's IP range.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    RequireHeaderSymmetry = false,
    // In production, restrict to known proxy IPs: KnownProxies = new() { IPAddress.Parse("10.0.0.1") }
});

// CORS middleware (ADR-0018, SEC-020, gateway.md): Handle preflight requests before HTTPS redirection.
// Preflight OPTIONS requests must be answered with CORS headers, not redirected.
app.UseCors("AllowLocalhost");

app.UseHttpsRedirection();

// SEC-010..013: ASP.NET Core authorization middleware (policies defined above). Evaluates
// policy requirements before controller actions run, throwing UnauthorizedAccessException when an
// actor lacks the required role/claims (surfaces as 403 Forbidden through ApiExceptionHandler).
// Fine-grained record-level authorization remains in Facade/IClientAuthorization/IProjectAuthorization/
// ITaskAuthorization after coarse authentication is confirmed.
app.UseAuthentication();
app.UseAuthorization();

// API-006: OpenAPI document and Scalar.net interactive documentation UI
// SEC-010..013: APIs require authentication (401) and role-based authorization (403)
// ADR-0018: Cookie authentication with HTTPOnly, Secure, SameSite=Strict policies
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.OpenApiRoutePattern = "/openapi/{documentName}.json";
    options.Title = "Project Chicago - CRM API";
});

app.MapControllers();

app.Run();

public partial class Program;
