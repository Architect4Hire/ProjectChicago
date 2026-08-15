using System.ComponentModel.DataAnnotations;

namespace ProjectChicago.Identity.Core.Authorization.Contracts;

// Transport model for user creation request (SEC-004, SEC-010..016). Used by Controller to bind request
// and Facade/Business to validate/process. Never carries passwords beyond validation for strength;
// never logs credentials. Validated on shape (required fields, lengths, format) at Controller boundary;
// domain/policy validation (password strength, role existence) occurs in Business layer.
public class CreateUserViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [StringLength(256, ErrorMessage = "Email must not exceed 256 characters.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 128 characters.")]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Role is required.")]
    [StringLength(256, ErrorMessage = "Role name must not exceed 256 characters.")]
    public string RoleName { get; set; } = null!;
}
