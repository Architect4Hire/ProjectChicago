using ProjectChicago.Gateway.Correlation;
using ProjectChicago.Gateway.Auth;
using ProjectChicago.Gateway.Sessions;
using ProjectChicago.Gateway.Proxy;
using ProjectChicago.Gateway.Csrf;
using Microsoft.AspNetCore.HttpOverrides;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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

// Redis client for session storage (ADR-0018-superseding BFF: holds JWT tokens server-side).
builder.AddRedisClient("redis");

// Session store and identity client (BFF components: login/logout endpoints and bearer-token transform).
builder.Services.AddScoped<ISessionStore, RedisSessionStore>();
builder.Services.AddHttpClient<IdentityInternalClient>()
    .ConfigureHttpClient(client => client.BaseAddress = new Uri(identityUrl));

// CSRF protection via ASP.NET Core IAntiforgery (ADR-0018-superseding: double-submit token pattern).
builder.Services.AddAntiforgery();

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

app.UseCorrelation();

app.UseCors("AllowLocalhost");

// Forwarded headers middleware: Trust X-Forwarded-* headers so IsHttps is correct for cookie Secure flag.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    RequireHeaderSymmetry = false,
});

// HTTPS redirection at the gateway edge ensures all downstream communication uses HTTPS,
// preventing backend services from issuing redirects that break the proxy chain and CORS.
app.UseHttpsRedirection();

app.MapDefaultEndpoints();

// Bearer token injection middleware: injects JWT bearer tokens from Redis session into proxied requests (ADR-0018-superseding).
app.UseMiddleware<BearerTokenMiddleware>();

// CSRF validation middleware: checks double-submit tokens on mutating requests (ADR-0018-superseding).
app.UseMiddleware<CsrfValidationMiddleware>();

// BFF authentication endpoints: login/logout owned by Gateway, other /auth/* routes go through YARP.
AuthEndpoints.MapAuthEndpoints(app);

// Forward all configured routes through YARP (gateway.md step 5).
// Correlation middleware has already normalized and set headers (gateway.md step 4).
app.MapReverseProxy();

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
