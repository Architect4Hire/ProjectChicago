namespace ProjectChicago.Identity.Core.Authorization.Contracts;

// Login response contract (ADR-0018: CSRF token returned after successful login). Client stores token
// in memory and includes it in all mutation requests via X-CSRF-TOKEN header. ExpiresAt is the session
// cookie expiration time for client UI display (e.g., "Session expires in 30 minutes").
public class LoginServiceModel
{
    public required string Token { get; set; }

    public required DateTime ExpiresAt { get; set; }
}
