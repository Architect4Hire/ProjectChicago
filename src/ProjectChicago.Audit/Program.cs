using ProjectChicago.Audit.Core.Persistence;
using ProjectChicago.ServiceDefaults.Correlation;
using ProjectChicago.ServiceDefaults.Errors;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Aspire SQL Server EF Core client integration: wires AuditDbContext to the Aspire-injected
// "AuditDb" connection with resilience, health check, and telemetry defaults.
builder.AddSqlServerDbContext<AuditDbContext>("AuditDb");

// ERROR-001..005: shared Problem Details/exception handling and the HTTP request/actor context
// adapter are host-owned composition (backend.md), not part of AddServiceDefaults.
builder.Services.AddHttpRequestContext();
builder.Services.AddApiExceptionHandling();

// MVC controllers are the only HTTP application edge. AddOpenApi satisfies API-006 - every
// public API contract is documented through OpenAPI.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapControllers();

app.Run();

public partial class Program;
