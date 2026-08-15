using System.ComponentModel.DataAnnotations;

namespace ProjectChicago.Identity.Core.Authorization.Contracts;

// Transport model for add role request (SEC-004, SEC-010..016). Used by Controller to bind request
// and Facade/Business to validate/process. Role name must be one of the approved roles.
public class AddRoleViewModel
{
    [Required(ErrorMessage = "Role name is required.")]
    [StringLength(256, ErrorMessage = "Role name must not exceed 256 characters.")]
    public string RoleName { get; set; } = null!;
}
