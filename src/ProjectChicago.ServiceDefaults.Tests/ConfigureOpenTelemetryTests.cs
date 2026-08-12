using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace ProjectChicago.ServiceDefaults.Tests;

public class ConfigureOpenTelemetryTests
{
    [Fact]
    public void ConfigureOpenTelemetry_RegistersTracerAndMeterProvidersExactlyOnce()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ApplicationName = "test-service",
            EnvironmentName = "Development"
        });

        builder.ConfigureOpenTelemetry();

        using var host = builder.Build();

        var tracerProviders = host.Services.GetServices<TracerProvider>().ToList();
        var meterProviders = host.Services.GetServices<MeterProvider>().ToList();

        Assert.Single(tracerProviders);
        Assert.Single(meterProviders);
    }
}
