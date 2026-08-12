using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ProjectChicago.ServiceDefaults.Tests;

public class OpenTelemetryExporterConfigurationTests
{
    [Fact]
    public void HasOtlpExporterEndpoint_WhenConfigured_ReturnsTrue()
    {
        var configuration = BuildConfiguration(("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317"));

        Assert.True(Extensions.HasOtlpExporterEndpoint(configuration));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HasOtlpExporterEndpoint_WhenAbsentOrBlank_ReturnsFalse(string? value)
    {
        var configuration = BuildConfiguration(("OTEL_EXPORTER_OTLP_ENDPOINT", value));

        Assert.False(Extensions.HasOtlpExporterEndpoint(configuration));
    }

    [Fact]
    public void HasAzureMonitorConnectionString_WhenConfigured_ReturnsTrue()
    {
        var configuration = BuildConfiguration(("APPLICATIONINSIGHTS_CONNECTION_STRING", "InstrumentationKey=test"));

        Assert.True(Extensions.HasAzureMonitorConnectionString(configuration));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HasAzureMonitorConnectionString_WhenAbsentOrBlank_ReturnsFalse(string? value)
    {
        var configuration = BuildConfiguration(("APPLICATIONINSIGHTS_CONNECTION_STRING", value));

        Assert.False(Extensions.HasAzureMonitorConnectionString(configuration));
    }

    [Fact]
    public void HasAzureMonitorConnectionString_DoesNotActivateFromOtlpEndpointAlone()
    {
        var configuration = BuildConfiguration(("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317"));

        Assert.False(Extensions.HasAzureMonitorConnectionString(configuration));
    }

    [Fact]
    public void ResolveServiceVersion_ReturnsNonEmptyValue()
    {
        var version = Extensions.ResolveServiceVersion();

        Assert.False(string.IsNullOrWhiteSpace(version));
    }

    private static IConfiguration BuildConfiguration(params (string Key, string? Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.ToDictionary(e => e.Key, e => e.Value))
            .Build();
}
