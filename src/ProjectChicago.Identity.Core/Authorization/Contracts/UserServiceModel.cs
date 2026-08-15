namespace ProjectChicago.Identity.Core.Authorization.Contracts;

// Transport model for user creation response (SEC-004, SEC-010..016). Contains publicly safe user information
// without credentials, password hashes, or reset tokens. Never includes passwords or authentication secrets.
public class UserServiceModel
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = null!;

    public string RoleName { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
}
