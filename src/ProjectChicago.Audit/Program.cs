using ProjectChicago.Audit.Core.Contracts;
using ProjectChicago.Audit.Core.Data;
using ProjectChicago.Audit.Core.Facades;
using ProjectChicago.Audit.Core.Persistence;
using ProjectChicago.Audit.Core.Repositories;
using ProjectChicago.Audit.Core.Business;
using ProjectChicago.ServiceDefaults.Correlation;
using ProjectChicago.ServiceDefaults.Errors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Aspire SQL Server EF Core client integration: wires AuditDbContext to the Aspire-injected
// "AuditDb" connection with resilience, health check, and telemetry defaults.
builder.AddSqlServerDbContext<AuditDbContext>("AuditDb");

// ERROR-001..005: shared Problem Details/exception handling and the HTTP request/actor context
// adapter are host-owned composition (backend.md), not part of AddServiceDefaults.
builder.Services.AddHttpRequestContext();
builder.Services.AddApiExceptionHandling();

// JWT bearer authentication (ADR-0018-superseding, BFF pattern: validates JWT tokens injected by the Gateway).
// The Gateway holds JWT tokens server-side in Redis and injects Authorization: Bearer headers on proxied requests.
// Audit never receives the signing key (it stays with Identity); Audit validates using the shared public Issuer/Audience.
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
        options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
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

// SEC-012/SEC-013: Define authorization policies for Audit read access (AUDIT-001..008, ADR-0016).
// Audit.Read restricted to privileged roles (Administrator, Manager, and any Support-like roles
// that may be added in the future). Policy is evaluated by ASP.NET Core authorization middleware
// before controller actions run.
builder.Services.AddAuthorization(options =>
{
    // Audit.Read: view-only access to audit entries. Restricted to Administrator and Manager roles
    // who may need to troubleshoot issues, investigate compliance questions, or support users.
    // Contributors and ReadOnly users do not have audit visibility by default.
    options.AddPolicy(AuditApiContract.RequiredReadAuthorizationPolicy,
        policy => policy.RequireRole("Administrator", "Manager"));
});

// Domain onion composition (onion-boundaries.md): Data/Repository, Business, Facade layers
// for audit read use cases. Controllers reference only Facade interfaces, which reference
// only Business interfaces, which reference only Data interfaces.
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
builder.Services.AddScoped<IAuditData, AuditData>();
builder.Services.AddScoped<IAuditReadBusiness, AuditReadBusiness>();
builder.Services.AddScoped<IAuditReadFacade, AuditReadFacade>();

// MVC controllers are the only HTTP application edge. AddOpenApi satisfies API-006 - every
// public API contract is documented through OpenAPI.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply pending EF Core migrations (DATA-034: migrations through controlled deployment).
// Runs synchronously on startup to ensure schema is ready before the service serves requests.
// If migration fails, service startup fails (correct behavior for schema corruption/errors).
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    await dbContext.Database.MigrateAsync();
    Console.WriteLine("[Audit] ✓ Database migrations applied successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"[Audit] ✗ Database migration failed: {ex.Message}");
    throw;
}

// ERROR-001..005, TRACE-001..007, LOG-001..006: Exception handler middleware (ProblemDetails/ApiExceptionHandler)
// processes all unhandled exceptions and structured errors, returning safe responses with trace ID references
// for support correlation. StatusCodePages converts bare status codes (404, etc.) into the same consistent
// ProblemDetails shape. Both preserve trace context (W3C traceparent) and do not leak internal details/stack
// traces to external callers (ERROR-002). Structured logging includes traceId for end-to-end correlation.
app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapDefaultEndpoints();

// SEC-012/SEC-013: ASP.NET Core authorization middleware (policies defined above).
// Evaluates policy requirements before controller actions run.
app.UseAuthentication();
app.UseAuthorization();

// API-006: OpenAPI document and Scalar.net interactive documentation UI
// SEC-012: Audit.Read policy enforced by [Authorize(Policy = ...)] middleware; Admin/Manager roles only
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.OpenApiRoutePattern = "/openapi/{documentName}.json";
    options.Title = "Project Chicago - Audit API";
});

app.MapControllers();

app.Run();

public partial class Program;
