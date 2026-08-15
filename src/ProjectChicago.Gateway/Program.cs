using ProjectChicago.Gateway.Correlation;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// YARP reverse proxy configuration for routing public API paths to service hosts via service discovery
// (SEC-020, gateway.md: preserve correlation headers, route to owning service).
// Configuration loaded from appsettings.json with named clusters/routes for each bounded service.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCorrelation();

app.MapDefaultEndpoints();

// Forward all configured routes through YARP (gateway.md step 5).
// Correlation middleware has already normalized and set headers (gateway.md step 4).
app.MapReverseProxy();

app.MapGet("/", () => "Hello World!");

app.Run();
