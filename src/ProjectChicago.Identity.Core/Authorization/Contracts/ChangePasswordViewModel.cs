using System.ComponentModel.DataAnnotations;

namespace ProjectChicago.Identity.Core.Authorization.Contracts;

// Transport model for password change request (SEC-004, SEC-005).
// Used by Controller to bind request and Facade/Business to validate/process.
// Never logs password values. Validated on shape (required fields, lengths) at Controller boundary;
// domain/policy validation (current password correctness, new password strength) occurs in Business layer.
public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Current password is required.")]
    [StringLength(128, ErrorMessage = "Current password must not exceed 128 characters.")]
    public string CurrentPassword { get; set; } = null!;

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "New password must be between 8 and 128 characters.")]
    public string NewPassword { get; set; } = null!;

    [Required(ErrorMessage = "Password confirmation is required.")]
    [StringLength(128, ErrorMessage = "Password confirmation must not exceed 128 characters.")]
    [Compare("NewPassword", ErrorMessage = "New password and confirmation must match.")]
    public string ConfirmPassword { get; set; } = null!;
}
