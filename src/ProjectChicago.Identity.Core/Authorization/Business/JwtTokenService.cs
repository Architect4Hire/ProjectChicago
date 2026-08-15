using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;

namespace ProjectChicago.Identity.Core.Authorization.Business;

/// <summary>
/// JWT token service for ADR-0018-superseding BFF design: mints short-lived access tokens and
/// longer-lived refresh tokens with distinct audiences, validates refresh tokens outside the
/// ASP.NET Core pipeline. Gateway holds tokens server-side in Redis; they never reach the browser.
/// </summary>
public sealed class JwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly string _accessAudience;
    private readonly string _refreshAudience;
    private readonly int _accessTokenLifetimeMinutes;
    private readonly int _refreshTokenLifetimeDays;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        var jwtConfig = _configuration.GetSection("Jwt");
        var signingKeyValue = jwtConfig["SigningKey"]
            ?? throw new InvalidOperationException("JWT signing key not configured (Jwt:SigningKey)");

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKeyValue));
        _issuer = jwtConfig["Issuer"] ?? throw new InvalidOperationException("JWT issuer not configured (Jwt:Issuer)");
        _accessAudience = jwtConfig["Audience"] ?? throw new InvalidOperationException("JWT audience not configured (Jwt:Audience)");
        _refreshAudience = jwtConfig["RefreshAudience"] ?? throw new InvalidOperationException("JWT refresh audience not configured (Jwt:RefreshAudience)");

        if (!int.TryParse(jwtConfig["AccessTokenLifetimeMinutes"] ?? "10", out _accessTokenLifetimeMinutes))
            _accessTokenLifetimeMinutes = 10;

        if (!int.TryParse(jwtConfig["RefreshTokenLifetimeDays"] ?? "14", out _refreshTokenLifetimeDays))
            _refreshTokenLifetimeDays = 14;
    }

    /// <summary>
    /// Mint a short-lived JWT access token with user identity and role claims.
    /// Audience: projectchicago.services (validated by CRM, Audit, Identity when self-validating).
    /// </summary>
    public (string Token, DateTime ExpiresAtUtc) MintAccessToken(ApplicationUser user, IList<string> roles)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_accessTokenLifetimeMinutes);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim("email", user.Email ?? ""),
            new Claim("jti", Guid.NewGuid().ToString()),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _accessAudience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256));

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenString = tokenHandler.WriteToken(token);

        return (tokenString, expiresAtUtc);
    }

    /// <summary>
    /// Mint a long-lived JWT refresh token with minimal claims (sub, jti only).
    /// Audience: projectchicago.identity.refresh (validated manually inside /auth/refresh handler only).
    /// Refresh tokens are stored server-side in Redis by the Gateway and never sent to the browser.
    /// </summary>
    public (string Token, DateTime ExpiresAtUtc) MintRefreshToken(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var expiresAtUtc = DateTime.UtcNow.AddDays(_refreshTokenLifetimeDays);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("jti", Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _refreshAudience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256));

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenString = tokenHandler.WriteToken(token);

        return (tokenString, expiresAtUtc);
    }

    /// <summary>
    /// Manually validate a refresh token outside the ASP.NET Core authentication pipeline.
    /// Returns the user ID (sub) from the token, or throws InvalidOperationException on failure.
    /// Used by Identity's /auth/refresh endpoint to validate the refresh token before minting a new access token.
    /// </summary>
    public Guid ValidateRefreshToken(string refreshToken)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(refreshToken);

        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _refreshAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        try
        {
            var principal = tokenHandler.ValidateToken(refreshToken, validationParameters, out var validatedToken);
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim?.Value is null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                throw new InvalidOperationException("Refresh token is missing or invalid user ID claim");
            }

            return userId;
        }
        catch (SecurityTokenException ex)
        {
            throw new InvalidOperationException($"Refresh token validation failed: {ex.Message}", ex);
        }
    }
}
