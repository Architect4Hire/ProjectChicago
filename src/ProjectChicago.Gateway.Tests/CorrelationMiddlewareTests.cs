using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectChicago.Gateway.Correlation;
using ProjectChicago.ServiceDefaults.Correlation;
using Xunit;

namespace ProjectChicago.Gateway.Tests;

public class CorrelationMiddlewareTests
{
    [Fact]
    public async Task NoIncomingCorrelationHeader_GeneratesFreshIdAndReturnsItToCaller()
    {
        var headersBox = new StrongBox<IHeaderDictionary>();
        using var host = await CreateHostAsync(headersBox);
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/");

        Assert.True(response.Headers.TryGetValues(HttpRequestContextFactory.CorrelationIdHeaderName, out var values));
        var correlationId = Assert.Single(values);
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
    }

    [Fact]
    public async Task ValidIncomingCorrelationHeader_IsPropagatedDownstreamAndEchoedToCaller()
    {
        var headersBox = new StrongBox<IHeaderDictionary>();
        using var host = await CreateHostAsync(headersBox);
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(HttpRequestContextFactory.CorrelationIdHeaderName, "caller-correlation-1");

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(HttpRequestContextFactory.CorrelationIdHeaderName, out var values));
        Assert.Equal("caller-correlation-1", Assert.Single(values));
        Assert.Equal("caller-correlation-1", (string)headersBox.Value![HttpRequestContextFactory.CorrelationIdHeaderName]!);
    }

    [Fact]
    public async Task OversizedCorrelationHeader_IsReplacedWithFreshSafeIdInBothDownstreamRequestAndResponse()
    {
        var headersBox = new StrongBox<IHeaderDictionary>();
        using var host = await CreateHostAsync(headersBox);
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(HttpRequestContextFactory.CorrelationIdHeaderName, new string('a', 500));

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(HttpRequestContextFactory.CorrelationIdHeaderName, out var values));
        var correlationId = Assert.Single(values);
        Assert.True(correlationId.Length < 500);
        Assert.Equal(correlationId, (string)headersBox.Value![HttpRequestContextFactory.CorrelationIdHeaderName]!);
    }

    [Fact]
    public async Task InvalidCorrelationHeaderWithControlCharacters_IsReplacedWithFreshSafeId()
    {
        var headersBox = new StrongBox<IHeaderDictionary>();
        using var host = await CreateHostAsync(headersBox);
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation(HttpRequestContextFactory.CorrelationIdHeaderName, "legit-id\r\nX-Injected: true");

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(HttpRequestContextFactory.CorrelationIdHeaderName, out var values));
        var correlationId = Assert.Single(values);
        Assert.DoesNotContain('\r', correlationId);
        Assert.DoesNotContain('\n', correlationId);
        Assert.NotEqual("legit-id\r\nX-Injected: true", correlationId);
        Assert.Equal(correlationId, (string)headersBox.Value![HttpRequestContextFactory.CorrelationIdHeaderName]!);
    }

    private static async Task<IHost> CreateHostAsync(StrongBox<IHeaderDictionary> capturedDownstreamHeaders)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder => webBuilder
                .UseTestServer()
                .ConfigureServices(services => services.AddLogging())
                .Configure(app =>
                {
                    app.UseCorrelation();
                    app.Run(context =>
                    {
                        capturedDownstreamHeaders.Value = context.Request.Headers;
                        return context.Response.WriteAsync("ok");
                    });
                }))
            .StartAsync();

        return host;
    }
}
