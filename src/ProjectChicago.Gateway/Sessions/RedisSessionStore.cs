using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;

namespace ProjectChicago.Gateway.Sessions;

/// <summary>
/// Redis-backed session store for the BFF pattern (ADR-0018-superseding).
/// Sessions are JSON-serialized and stored with TTL tied to refresh token expiry.
/// </summary>
public class RedisSessionStore : ISessionStore
{
    private readonly IConnectionMultiplexer _redis;
    private const string SessionKeyPrefix = "session:";

    public RedisSessionStore(IConnectionMultiplexer redis)
    {
        ArgumentNullException.ThrowIfNull(redis);
        _redis = redis;
    }

    /// <summary>Create a new session with a random 256-bit session ID (base64 URL-safe).</summary>
    public async Task<string> CreateAsync(GatewaySession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var sessionId = GenerateSessionId();
        var key = GetSessionKey(sessionId);
        var json = JsonSerializer.Serialize(session);
        var ttl = session.RefreshTokenExpiresAtUtc - DateTime.UtcNow;

        var db = _redis.GetDatabase();
        await db.StringSetAsync(key, json, ttl, flags: CommandFlags.None);

        return sessionId;
    }

    /// <summary>Retrieve a session by ID; Redis expiration handles automatic cleanup.</summary>
    public async Task<GatewaySession?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        var key = GetSessionKey(sessionId);
        var db = _redis.GetDatabase();
        var json = await db.StringGetAsync(key);

        if (!json.HasValue)
            return null;

        return JsonSerializer.Deserialize<GatewaySession>(json.ToString());
    }

    /// <summary>Update a session (e.g., refresh token rotation) and extend its TTL.</summary>
    public async Task UpdateAsync(string sessionId, GatewaySession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID cannot be empty", nameof(sessionId));

        var key = GetSessionKey(sessionId);
        var json = JsonSerializer.Serialize(session);
        var ttl = session.RefreshTokenExpiresAtUtc - DateTime.UtcNow;

        var db = _redis.GetDatabase();
        await db.StringSetAsync(key, json, ttl, flags: CommandFlags.None);
    }

    /// <summary>Delete a session (logout).</summary>
    public async Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return;

        var key = GetSessionKey(sessionId);
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(key);
    }

    /// <summary>Generate a cryptographically random session ID (URL-safe base64).</summary>
    private static string GenerateSessionId()
    {
        var buffer = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(buffer);
        return Convert.ToBase64String(buffer).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string GetSessionKey(string sessionId) => $"{SessionKeyPrefix}{sessionId}";
}
