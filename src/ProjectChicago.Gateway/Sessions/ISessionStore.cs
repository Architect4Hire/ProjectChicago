namespace ProjectChicago.Gateway.Sessions;

/// <summary>
/// Abstracts session storage for the BFF pattern (ADR-0018-superseding).
/// Implementations hold server-side session records (tokens, user info) in Redis.
/// </summary>
public interface ISessionStore
{
    /// <summary>Create a new session and return its opaque ID.</summary>
    Task<string> CreateAsync(GatewaySession session, CancellationToken cancellationToken = default);

    /// <summary>Retrieve a session by ID; returns null if not found or expired.</summary>
    Task<GatewaySession?> GetAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Update an existing session (e.g., refresh token rotation) and extend its TTL.</summary>
    Task UpdateAsync(string sessionId, GatewaySession session, CancellationToken cancellationToken = default);

    /// <summary>Delete a session (e.g., on logout).</summary>
    Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default);
}
