using ProjectChicago.Gateway.Correlation;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// DEBUG: Log all environment variables and configuration keys to understand Aspire injection
Console.WriteLine("\n=== GATEWAY STARTUP DEBUG ===");
Console.WriteLine("Environment Variables (services*):");
foreach (var env in Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process).Cast<System.Collections.DictionaryEntry>())
{
    var key = env.Key.ToString();
    if (key?.Contains("services", StringComparison.OrdinalIgnoreCase) == true)
    {
        Console.WriteLine($"  {key} = {env.Value}");
    }
}

Console.WriteLine("\nConfiguration Keys (ReverseProxy*):");
var allKeys = builder.Configuration.AsEnumerable().Where(kvp => kvp.Key?.Contains("Reverse", StringComparison.OrdinalIgnoreCase) == true);
foreach (var kvp in allKeys)
{
    Console.WriteLine($"  {kvp.Key} = {kvp.Value}");
}

Console.WriteLine("\nConfiguration Keys (services*):");
var serviceKeys = builder.Configuration.AsEnumerable().Where(kvp => kvp.Key?.Contains("services", StringComparison.OrdinalIgnoreCase) == true);
foreach (var kvp in serviceKeys)
{
    Console.WriteLine($"  {kvp.Key} = {kvp.Value}");
}

// Resolve Aspire-injected service URLs from configuration (ADR-0015, SEC-020, gateway.md).
// Aspire WithReference() injects endpoints as services__<name>__http; we resolve them before YARP initializes.
var crmUrl = ResolveAspireServiceUrl(builder.Configuration, "crm");
var identityUrl = ResolveAspireServiceUrl(builder.Configuration, "identity");
var auditUrl = ResolveAspireServiceUrl(builder.Configuration, "audit");

Console.WriteLine($"\nResolved URLs:");
Console.WriteLine($"  CRM: {crmUrl}");
Console.WriteLine($"  Identity: {identityUrl}");
Console.WriteLine($"  Audit: {auditUrl}");
Console.WriteLine("============================\n");

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

// YARP reverse proxy configuration for routing public API paths to service hosts via Aspire service discovery
// (SEC-020, gateway.md: preserve correlation headers, route to owning service).
// Resolved Aspire URLs are injected into cluster destinations before YARP loads configuration (gateway.md).
var reverseProxyConfig = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        { "ReverseProxy:Clusters:crm:Destinations:crm-primary:Address", crmUrl },
        { "ReverseProxy:Clusters:identity:Destinations:identity-primary:Address", identityUrl },
        { "ReverseProxy:Clusters:audit:Destinations:audit-primary:Address", auditUrl },
    })
    .Build();

builder.Configuration.AddConfiguration(reverseProxyConfig);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Verify YARP configuration loaded correctly (DEBUG)
Console.WriteLine("\nYARP Loaded Clusters:");
var proxyConfig = app.Services.GetRequiredService<IProxyConfigProvider>().GetConfig();
foreach (var cluster in proxyConfig.Clusters)
{
    Console.WriteLine($"  {cluster.ClusterId}:");
    foreach (var dest in cluster.Destinations)
    {
        Console.WriteLine($"    {dest.Key} -> {dest.Value.Address}");
    }
}
Console.WriteLine();

app.UseCorrelation();

app.UseCors("AllowLocalhost");

// HTTPS redirection at the gateway edge ensures all downstream communication uses HTTPS,
// preventing backend services from issuing redirects that break the proxy chain and CORS.
app.UseHttpsRedirection();

app.MapDefaultEndpoints();

// Forward all configured routes through YARP (gateway.md step 5).
// Correlation middleware has already normalized and set headers (gateway.md step 4).
app.MapReverseProxy();

app.MapGet("/", () => "Hello World!");

app.Run();

// Resolve Aspire-injected service URLs from configuration (ADR-0015, SEC-020, gateway.md).
// Aspire WithReference() injects URLs as services:<name>:http:0 or services__<name>__http__0 (array index format).
// Use HTTP for internal YARP→service routing in development; HTTPS is for production.
// Falls back to service name for local DNS resolution if Aspire injection not found.
static string ResolveAspireServiceUrl(IConfiguration configuration, string serviceName)
{
    Console.WriteLine($"\nResolving {serviceName}:");

    // Try multiple key formats that Aspire injects (Aspire uses array index :0 for the first/only endpoint).
    // Prefer HTTP for internal gateway→service routing (simpler in development; HTTPS for production).
    var checks = new[]
    {
        ($"services:{serviceName}:http:0", configuration[$"services:{serviceName}:http:0"]),
        ($"services:{serviceName}:http", configuration[$"services:{serviceName}:http"]),
        ($"services__{serviceName}__http__0", configuration[$"services__{serviceName}__http__0"]),
        ($"services__{serviceName}__http", configuration[$"services__{serviceName}__http"]),
        ($"Services:{serviceName}:Http:0", configuration[$"Services:{serviceName}:Http:0"]),
        ($"ENV:services__{serviceName}__http__0", Environment.GetEnvironmentVariable($"services__{serviceName}__http__0")),
        ($"ENV:services__{serviceName}__http", Environment.GetEnvironmentVariable($"services__{serviceName}__http")),
        ($"ENV:SERVICES__{serviceName.ToUpper()}__HTTP__0", Environment.GetEnvironmentVariable($"SERVICES__{serviceName.ToUpper()}__HTTP__0")),
        ($"ENV:SERVICES__{serviceName.ToUpper()}__HTTP", Environment.GetEnvironmentVariable($"SERVICES__{serviceName.ToUpper()}__HTTP")),
    };

    foreach (var (key, value) in checks)
    {
        Console.WriteLine($"  {key} = {value ?? "(not found)"}");
        if (!string.IsNullOrEmpty(value))
        {
            Console.WriteLine($"  -> RESOLVED: {value}");
            return value;
        }
    }

    // Fallback to service name for local DNS resolution (used in local Aspire orchestration)
    var fallback = $"https://{serviceName}";
    Console.WriteLine($"  -> FALLBACK: {fallback}");
    return fallback;
}
