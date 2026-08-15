using System.ComponentModel.DataAnnotations;

namespace ProjectChicago.Identity.Core.Authorization.Contracts;

// Login request contract (ADR-0018: cookie authentication). Normalized and validated by transport layer
// before entering the service boundary. Username is trimmed and lowercased for lookup.
public class LoginViewModel
{
    [Required]
    [StringLength(256)]
    public string UserName { get; set; } = "";

    [Required]
    [StringLength(128)]
    public string Password { get; set; } = "";
}
