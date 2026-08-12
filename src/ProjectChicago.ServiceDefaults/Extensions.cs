using System.Reflection;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.SqlClient;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: builder.Environment.ApplicationName,
                    serviceVersion: ResolveServiceVersion())
                .AddAttributes([
                    new("deployment.environment", builder.Environment.EnvironmentName)
                ]))
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation()
                    // EF Core/SQL Server dependency calls execute through Microsoft.Data.SqlClient; this captures them.
                    .AddSqlClientInstrumentation(ConfigureSqlClientInstrumentation)
                    // Azure SDK clients (e.g. Azure.Messaging.ServiceBus) publish their own W3C-compatible
                    // Activities under the "Azure.*" ActivitySource family; no dedicated instrumentation package exists.
                    .AddSource("Azure.*");
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Local development: Aspire injects OTEL_EXPORTER_OTLP_ENDPOINT so traces/metrics/logs flow to the Aspire Dashboard.
        if (HasOtlpExporterEndpoint(builder.Configuration))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Production: Azure Monitor/Application Insights is the SPOG backend (ADR-0011/ADR-0021).
        // Activated only when a connection string is present in configuration; never hardcoded here.
        if (HasAzureMonitorConnectionString(builder.Configuration))
        {
            builder.Services.AddOpenTelemetry().UseAzureMonitor();
        }

        return builder;
    }

    internal static bool HasOtlpExporterEndpoint(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

    internal static bool HasAzureMonitorConnectionString(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]);

    internal static string ResolveServiceVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

    internal static void ConfigureSqlClientInstrumentation(SqlClientTraceInstrumentationOptions options)
    {
        // This instrumentation never captures SQL statement text or parameter values by default.
        // Record exceptions (OTEL-003) but do not wire an enrichment callback that could
        // reintroduce sensitive command/parameter data into spans.
        options.RecordException = true;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks(HealthEndpointPath);

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
