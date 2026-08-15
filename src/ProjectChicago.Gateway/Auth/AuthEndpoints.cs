using Microsoft.AspNetCore.Antiforgery;
using ProjectChicago.Gateway.Sessions;

namespace ProjectChicago.Gateway.Auth;

/// <summary>
/// Authentication endpoints for the BFF pattern (ADR-0018-superseding).
/// Maps /auth/login and /auth/logout; other /auth/* routes flow through YARP to Identity.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>Request model for login (matches Identity's LoginViewModel).</summary>
    public record LoginRequest(string Email, string Password);

    /// <summary>Response model for login/refresh (user info only, no tokens exposed to browser).</summary>
    public record LoginResponse(Guid UserId, string Email, string UserName, List<string> Roles);

    public static void MapAuthEndpoints(WebApplication app)
    {
        var authGroup = app.MapGroup("/auth")
            .WithTags("Authentication");

        authGroup.MapPost("/login", HandleLoginAsync)
            .WithName("BffLogin")
            .WithDescription("Login with email and password. Issues HttpOnly session cookie and returns CSRF token via header.")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous();

        authGroup.MapPost("/logout", HandleLogoutAsync)
            .WithName("BffLogout")
            .WithDescription("Logout: delete Redis session and clear HttpOnly session cookie.")
            .Produces(StatusCodes.Status200OK)
            .RequireAuthorization();
    }

    private static async Task<IResult> HandleLoginAsync(
        LoginRequest request,
        IdentityInternalClient identityClient,
        ISessionStore sessionStore,
        IAntiforgery antiforgery,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            // Call Identity's /auth/login endpoint
            var identityResponse = await identityClient.LoginAsync(
                request.Email,
                request.Password,
                cancellationToken);

            // Create a server-side session in Redis, holding the tokens and user info
            var session = new GatewaySession
            {
                UserId = identityResponse.User.UserId,
                Email = identityResponse.User.Email,
                UserName = identityResponse.User.UserName,
                Roles = identityResponse.User.Roles,
                AccessToken = identityResponse.AccessToken,
                AccessTokenExpiresAtUtc = identityResponse.AccessTokenExpiresAtUtc,
                RefreshToken = identityResponse.RefreshToken,
                RefreshTokenExpiresAtUtc = identityResponse.RefreshTokenExpiresAtUtc,
            };

            var sessionId = await sessionStore.CreateAsync(session, cancellationToken);

            // Set HttpOnly session cookie with the opaque session ID (never the tokens)
            httpContext.Response.Cookies.Append(
                ".ProjectChicago.SessionId",
                sessionId,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = httpContext.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    MaxAge = session.RefreshTokenExpiresAtUtc - DateTime.UtcNow,
                });

            // Issue CSRF token via double-submit pattern (returned in response header, not body)
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            httpContext.Response.Headers["X-CSRF-TOKEN"] = tokens.RequestToken ?? "";

            // Return user info only (no tokens to browser)
            return Results.Ok(new LoginResponse(
                identityResponse.User.UserId,
                identityResponse.User.Email,
                identityResponse.User.UserName,
                identityResponse.User.Roles));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return Results.Unauthorized();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[ERROR] Identity login failed: {ex.Message} | StatusCode: {ex.StatusCode} | InnerException: {ex.InnerException?.Message}");
            return Results.Problem($"Identity service error: {ex.Message}", statusCode: StatusCodes.Status502BadGateway);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Unexpected login error: {ex.GetType().Name} - {ex.Message} | StackTrace: {ex.StackTrace}");
            return Results.Problem($"Unexpected error: {ex.Message}", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> HandleLogoutAsync(
        ISessionStore sessionStore,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Extract session ID from cookie
        if (httpContext.Request.Cookies.TryGetValue(".ProjectChicago.SessionId", out var sessionId))
        {
            // Delete the Redis session
            await sessionStore.DeleteAsync(sessionId, cancellationToken);
        }

        // Clear the session cookie
        httpContext.Response.Cookies.Delete(".ProjectChicago.SessionId");

        return Results.Ok(new { message = "Logged out successfully" });
    }
}
