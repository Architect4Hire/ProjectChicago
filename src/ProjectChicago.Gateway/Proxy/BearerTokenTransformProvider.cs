using ProjectChicago.Gateway.Sessions;
using ProjectChicago.Gateway.Auth;

namespace ProjectChicago.Gateway.Proxy;

/// <summary>
/// Middleware that injects JWT bearer tokens into proxied requests for the BFF pattern (ADR-0018-superseding).
/// Also handles inline token refresh when the access token is near expiry.
/// </summary>
public class BearerTokenMiddleware
{
    private readonly RequestDelegate _next;
    private const int RefreshSkewSeconds = 60;

    public BearerTokenMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext, ISessionStore sessionStore, IdentityInternalClient identityClient)
    {
        // Try to read the session ID from the cookie
        if (httpContext.Request.Cookies.TryGetValue(".ProjectChicago.SessionId", out var sessionId) &&
            !string.IsNullOrWhiteSpace(sessionId))
        {
            // Retrieve the session from Redis
            var session = await sessionStore.GetAsync(sessionId);
            if (session is not null)
            {
                // Check if the access token is near expiry (within 60 seconds)
                if (DateTime.UtcNow.AddSeconds(RefreshSkewSeconds) >= session.AccessTokenExpiresAtUtc)
                {
                    try
                    {
                        // Token is near expiry - refresh it inline
                        var refreshedResponse = await identityClient.RefreshAsync(session.RefreshToken);

                        // Update the session in Redis with the new token pair (token rotation)
                        var updatedSession = session with
                        {
                            AccessToken = refreshedResponse.AccessToken,
                            AccessTokenExpiresAtUtc = refreshedResponse.AccessTokenExpiresAtUtc,
                            RefreshToken = refreshedResponse.RefreshToken,
                            RefreshTokenExpiresAtUtc = refreshedResponse.RefreshTokenExpiresAtUtc,
                        };
                        await sessionStore.UpdateAsync(sessionId, updatedSession);

                        // Inject the new access token into the request
                        httpContext.Request.Headers.Authorization = $"Bearer {refreshedResponse.AccessToken}";
                    }
                    catch
                    {
                        // Refresh failed - clear the session and continue unauthenticated
                        await sessionStore.DeleteAsync(sessionId);
                        httpContext.Response.Cookies.Delete(".ProjectChicago.SessionId");
                    }
                }
                else
                {
                    // Access token is still valid - inject it
                    httpContext.Request.Headers.Authorization = $"Bearer {session.AccessToken}";
                }
            }
            else
            {
                // Session not found or expired - clear cookie
                httpContext.Response.Cookies.Delete(".ProjectChicago.SessionId");
            }
        }

        await _next(httpContext);
    }
}
