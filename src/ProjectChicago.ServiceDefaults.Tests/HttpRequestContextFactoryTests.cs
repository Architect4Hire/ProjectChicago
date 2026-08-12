using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProjectChicago.ServiceDefaults.Correlation;
using ProjectChicago.Shared.Correlation;
using Xunit;

namespace ProjectChicago.ServiceDefaults.Tests;

public class HttpRequestContextFactoryTests
{
    [Fact]
    public void Create_AuthenticatedUser_ResolvesActorIdAndRolesFromClaimsPrincipal()
    {
        var httpContext = new DefaultHttpContext
        {
            User = CreateAuthenticatedUser("user-123", "Sales", "Manager")
        };

        var context = HttpRequestContextFactory.Create(httpContext);

        Assert.Equal(ActorType.User, context.Actor.ActorType);
        Assert.Equal("user-123", context.Actor.ActorId);
        Assert.Equal(["Manager", "Sales"], context.Actor.Roles.OrderBy(r => r));
    }

    [Fact]
    public void Create_AnonymousRequest_ResolvesAnonymousActorWithNoId()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };

        var context = HttpRequestContextFactory.Create(httpContext);

        Assert.Equal(ActorType.Anonymous, context.Actor.ActorType);
        Assert.Null(context.Actor.ActorId);
        Assert.Empty(context.Actor.Roles);
    }

    [Fact]
    public void Create_AuthenticatedIdentityWithoutNameIdentifierClaim_FallsBackToAnonymous()
    {
        var identity = new ClaimsIdentity(authenticationType: "TestScheme");
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        var context = HttpRequestContextFactory.Create(httpContext);

        Assert.Equal(ActorType.Anonymous, context.Actor.ActorType);
        Assert.Null(context.Actor.ActorId);
    }

    [Fact]
    public void Create_UntrustedActorHeaders_AreIgnoredInFavorOfClaimsPrincipal()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        httpContext.Request.Headers["X-User-Id"] = "attacker-supplied-id";
        httpContext.Request.Headers["X-Roles"] = "Admin";

        var context = HttpRequestContextFactory.Create(httpContext);

        Assert.Equal(ActorType.Anonymous, context.Actor.ActorType);
        Assert.Null(context.Actor.ActorId);
        Assert.Empty(context.Actor.Roles);
    }

    [Fact]
    public void Create_ValidPropagatedHeaders_ArePropagatedVerbatim()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        httpContext.Request.Headers[HttpRequestContextFactory.CorrelationIdHeaderName] = "correlation-1";
        httpContext.Request.Headers[HttpRequestContextFactory.CausationIdHeaderName] = "causation-1";
        httpContext.Request.Headers[HttpRequestContextFactory.RequestIdHeaderName] = "request-1";

        var context = HttpRequestContextFactory.Create(httpContext);

        Assert.Equal("correlation-1", context.CorrelationId);
        Assert.Equal("causation-1", context.CausationId);
        Assert.Equal("request-1", context.RequestId);
    }

    [Fact]
    public void Create_MissingPropagatedHeaders_GeneratesFreshIdentifiers()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };

        var context = HttpRequestContextFactory.Create(httpContext);

        Assert.False(string.IsNullOrWhiteSpace(context.TraceId));
        Assert.False(string.IsNullOrWhiteSpace(context.CorrelationId));
        Assert.False(string.IsNullOrWhiteSpace(context.RequestId));
        Assert.Null(context.CausationId);
    }

    [Fact]
    public void Create_DuplicatedCorrelationHeader_IsTreatedAsMalformedAndIgnored()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        httpContext.Request.Headers.Append(HttpRequestContextFactory.CorrelationIdHeaderName, "first-value");
        httpContext.Request.Headers.Append(HttpRequestContextFactory.CorrelationIdHeaderName, "second-value");

        var context = HttpRequestContextFactory.Create(httpContext);

        Assert.NotEqual("first-value", context.CorrelationId);
        Assert.NotEqual("second-value", context.CorrelationId);
        Assert.False(string.IsNullOrWhiteSpace(context.CorrelationId));
    }

    [Fact]
    public void Create_OversizedCorrelationHeader_IsTreatedAsMalformedAndIgnored()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        httpContext.Request.Headers[HttpRequestContextFactory.CorrelationIdHeaderName] = new string('a', 500);

        var context = HttpRequestContextFactory.Create(httpContext);

        Assert.True(context.CorrelationId.Length < 500);
    }

    [Fact]
    public void Create_ControlCharacterInCorrelationHeader_IsTreatedAsMalformedAndIgnored()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        httpContext.Request.Headers[HttpRequestContextFactory.CorrelationIdHeaderName] = "legit-id\r\nX-Injected: true";

        var context = HttpRequestContextFactory.Create(httpContext);

        Assert.DoesNotContain('\r', context.CorrelationId);
        Assert.DoesNotContain('\n', context.CorrelationId);
        Assert.NotEqual("legit-id\r\nX-Injected: true", context.CorrelationId);
    }

    [Fact]
    public void Create_NullHttpContext_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => HttpRequestContextFactory.Create(null!));
    }

    private static ClaimsPrincipal CreateAuthenticatedUser(string actorId, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, actorId) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, authenticationType: "TestScheme");
        return new ClaimsPrincipal(identity);
    }
}
