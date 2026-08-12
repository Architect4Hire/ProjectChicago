using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.SqlClient;
using Xunit;

namespace ProjectChicago.ServiceDefaults.Tests;

public class SqlClientInstrumentationConfigurationTests
{
    [Fact]
    public void ConfigureSqlClientInstrumentation_DoesNotWireAnEnrichmentCallback()
    {
        var options = new SqlClientTraceInstrumentationOptions();

        Extensions.ConfigureSqlClientInstrumentation(options);

        Assert.Null(options.EnrichWithSqlCommand);
    }

    [Fact]
    public void ConfigureSqlClientInstrumentation_RecordsExceptions()
    {
        var options = new SqlClientTraceInstrumentationOptions();

        Extensions.ConfigureSqlClientInstrumentation(options);

        Assert.True(options.RecordException);
    }
}
