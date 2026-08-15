using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
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

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// ERROR-001..005, TRACE-001..007, LOG-001..006: Exception handler middleware (ProblemDetails/ApiExceptionHandler)
// processes all unhandled exceptions and structured errors before they reach the browser.
// StatusCodePages converts bare status codes (404, etc.) into the same ProblemDetails shape.
app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapDefaultEndpoints();

// API-006: OpenAPI document and Scalar.net interactive documentation UI
// ADR-0018: Cookie authentication (HTTPOnly, Secure, SameSite=Strict) and CSRF token support
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.OpenApiRoutePattern = "/openapi/{documentName}.json";
    options.Title = "Project Chicago - Identity API";
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
