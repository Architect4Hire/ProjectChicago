using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProjectChicago.ServiceDefaults.Correlation;
using ProjectChicago.Shared.Correlation;
using Xunit;

namespace ProjectChicago.ServiceDefaults.Tests;

public class HttpRequestContextAccessorTests
{
    [Fact]
    public void Current_ActiveHttpContext_ResolvesRequestContextViaFactory()
    {
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
        httpContext.Request.Headers[HttpRequestContextFactory.CorrelationIdHeaderName] = "correlation-1";
        var accessor = CreateAccessor(httpContext);

        var current = accessor.Current;

        Assert.Equal("correlation-1", current.CorrelationId);
    }

    [Fact]
    public void Current_CalledTwice_ReturnsTheSameResolvedValueRatherThanGeneratingFreshIdentifiers()
    {
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
        var accessor = CreateAccessor(httpContext);

        var first = accessor.Current;
        var second = accessor.Current;

        Assert.Equal(first.CorrelationId, second.CorrelationId);
        Assert.Equal(first.RequestId, second.RequestId);
    }

    [Fact]
    public void Current_NoActiveHttpContext_Throws()
    {
        var accessor = CreateAccessor(httpContext: null);

        Assert.Throws<InvalidOperationException>(() => accessor.Current);
    }

    private static HttpRequestContextAccessor CreateAccessor(HttpContext? httpContext)
    {
        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        return new HttpRequestContextAccessor(httpContextAccessor);
    }
}
