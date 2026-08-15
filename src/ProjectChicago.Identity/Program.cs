using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectChicago.Identity.Core.Authorization.Business;
using ProjectChicago.Identity.Core.Authorization.Data;
using ProjectChicago.Identity.Core.Authorization.Facade;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;
using ProjectChicago.Identity.Core.Persistence;
using ProjectChicago.Identity.Core.Authorization.Contracts;
using ProjectChicago.ServiceDefaults.Correlation;
using ProjectChicago.ServiceDefaults.Errors;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// HTTP request context (SEC-004, TRACE-001..007): Resolves correlation IDs, trace IDs, and authenticated actor
// from the inbound HTTP request. Adapts W3C Trace Context headers to ICurrentRequestContext for all downstream facades.
builder.Services.AddHttpRequestContext();

// ERROR-001..005: shared Problem Details/exception handling (TRACE-001..007, LOG-001..006) is host-owned composition
// (backend.md), not part of AddServiceDefaults. Call together with app.UseExceptionHandler() in the pipeline.
builder.Services.AddApiExceptionHandling();

// Aspire SQL Server EF Core client integration: connection string, resilience, health check, and
// telemetry come from the "IdentityDb" connection name injected by AppHost (aspire.md).
// IdentityDbContext itself never calls UseSqlServer or holds a connection string.
builder.AddSqlServerDbContext<IdentityDbContext>("IdentityDb");

// ASP.NET Core Identity framework services (SEC-001, SEC-003, SEC-004): UserManager, RoleManager,
// SignInManager, token providers, password hashers, and core account-security mechanics.
// Supports password hashing (SEC-002), lockout (SEC-004), claims/roles, and account tokens through
// the framework's built-in stores backed by IdentityDbContext and SQL Server (DATA-031).
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
    .AddEntityFrameworkStores<IdentityDbContext>()
    .AddDefaultTokenProviders();

// Cookie authentication (ADR-0018: HTTPOnly, Secure, SameSite=Strict session cookies).
// SignInManager issues cookies on successful login; middleware extracts claims on inbound requests.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.Cookie.Name = ".ProjectChicago.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
    });

// AntiForgery protection (ADR-0018: CSRF token validation on mutations).
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

// Authentication business, data, and facade (add-endpoint: layered architecture, SEC-005/AUDIT-001).
builder.Services.AddScoped<AuthenticationBusiness>();
builder.Services.AddScoped<AuthenticationData>();
builder.Services.AddScoped<AuthenticationFacade>();

// User management business, data, and facade (add-endpoint: layered architecture, SEC-004, SEC-010..016, AUDIT-001).
builder.Services.AddScoped<UserManagementBusiness>();
builder.Services.AddScoped<UserManagementData>();
builder.Services.AddScoped<UserManagementFacade>();

// User seeding for development (idempotent: skips if user exists).
builder.Services.AddScoped<ProjectChicago.Identity.Core.Authorization.Data.UserSeeder>();

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

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply pending EF Core migrations (DATA-034: migrations through controlled deployment).
// Runs synchronously on startup to ensure schema is ready before the service serves requests.
// If migration fails, service startup fails (correct behavior for schema corruption/errors).
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await dbContext.Database.MigrateAsync();
    Console.WriteLine("[Identity] ✓ Database migrations applied successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"[Identity] ✗ Database migration failed: {ex.Message}");
    throw;
}

// ERROR-001..005, TRACE-001..007, LOG-001..006: Exception handler middleware (ProblemDetails/ApiExceptionHandler)
// processes all unhandled exceptions and structured errors before they reach the browser.
// StatusCodePages converts bare status codes (404, etc.) into the same ProblemDetails shape.
app.UseExceptionHandler();
app.UseStatusCodePages();

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

app.MapDefaultEndpoints();

// API-006: OpenAPI document and Scalar.net interactive documentation UI
// ADR-0018: Cookie authentication (HTTPOnly, Secure, SameSite=Strict) and CSRF token support
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.OpenApiRoutePattern = "/openapi/{documentName}.json";
    options.Title = "Project Chicago - Identity API";
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Seed default user for local development (idempotent: skipped if user already exists).
await SeedDefaultUserAsync(app.Services);

app.Run();

async Task SeedDefaultUserAsync(IServiceProvider serviceProvider)
{
    using var scope = serviceProvider.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<ProjectChicago.Identity.Core.Authorization.Data.UserSeeder>();
    await seeder.SeedDefaultUserAsync("robert@architect4hire.com", "Chicago1974!!!", "Administrator");
}
