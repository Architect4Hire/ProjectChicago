using ProjectChicago.Crm.Core.Facades;
using ProjectChicago.Crm.Core.Persistence;
using ProjectChicago.Crm.Core.Repositories;
using ProjectChicago.ServiceDefaults.Correlation;
using ProjectChicago.ServiceDefaults.Errors;
using ProjectChicago.Crm.Core.Data;
using ProjectChicago.Crm.Core.Business;

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

// Domain onion composition (onion-boundaries.md): Data/Repository, Business, Facade layers
// for each bounded service's use cases. Controllers reference only Facade interfaces, which
// reference only Business interfaces, which reference only Data interfaces.
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<ITaskData, TaskData>();
builder.Services.AddScoped<ITaskBusiness, TaskBusiness>();
builder.Services.AddScoped<ITaskAuthorization, TaskAuthorization>();
builder.Services.AddScoped<ITaskFacade, TaskFacade>();

// Mechanism-neutral abstractions (onion-boundaries.md: "Facades depend only on ... abstractions
// such as current user, clock, cache, and correlation context"). The Clock abstraction lets
// Facades resolve "now" in a testable way without coupling to DateTime.UtcNow directly.
builder.Services.AddSingleton<IClock, Clock>();

// MVC controllers are the only HTTP application edge (onion-boundaries.md: "Use ASP.NET Core MVC
// controllers ...; do not add minimal API routes"). AddOpenApi/MapOpenApi satisfies API-006 - every
// public API contract is documented through OpenAPI, keyed by each action's stable OperationId
// (e.g. ClientsApiContract.CreateOperationId).
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

// Keeps status-code-only failures (no exception - e.g. an unmatched route today, or a future
// [ApiController] automatic 400) in the same ApiProblemDetailsCustomizer shape as exception-driven
// failures, instead of a bare empty-body status code (ERROR-001).
app.UseStatusCodePages();

app.MapDefaultEndpoints();

app.MapOpenApi();
app.MapControllers();

app.Run();

public partial class Program;
