using System.ComponentModel.DataAnnotations;

namespace ProjectChicago.Identity.Core.Authorization.Contracts;

// Transport model for password reset completion request (unauthenticated).
// User provides userId, reset token, and new password.
// Never logs token or password values. Validated on shape (required fields, lengths, match) at Controller boundary;
// token validity and password policy enforcement occurs in Business layer (SEC-004, SEC-005).
public class ResetPasswordViewModel
{
    [Required(ErrorMessage = "User ID is required.")]
    public Guid UserId { get; set; }

    [Required(ErrorMessage = "Reset token is required.")]
    [StringLength(512, ErrorMessage = "Reset token must not exceed 512 characters.")]
    public string Token { get; set; } = null!;

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "New password must be between 8 and 128 characters.")]
    public string NewPassword { get; set; } = null!;

    [Required(ErrorMessage = "Password confirmation is required.")]
    [StringLength(128, ErrorMessage = "Password confirmation must not exceed 128 characters.")]
    [Compare("NewPassword", ErrorMessage = "New password and confirmation must match.")]
    public string ConfirmPassword { get; set; } = null!;
}
