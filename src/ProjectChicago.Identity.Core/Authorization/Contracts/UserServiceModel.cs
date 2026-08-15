namespace ProjectChicago.Identity.Core.Authorization.Contracts;

// Transport model for user information (SEC-004, SEC-010..016). Contains publicly safe user information
// without credentials, password hashes, or reset tokens. Never includes passwords or authentication secrets.
public class UserServiceModel
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = null!;

    public string UserName { get; set; } = null!;

    public List<string> Roles { get; set; } = new();

    public DateTime CreatedAtUtc { get; set; }
}
