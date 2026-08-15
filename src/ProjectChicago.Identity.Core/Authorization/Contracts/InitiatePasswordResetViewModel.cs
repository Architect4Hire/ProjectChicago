using System.ComponentModel.DataAnnotations;

namespace ProjectChicago.Identity.Core.Authorization.Contracts;

// Transport model for password reset initiation request (admin-only).
// Admin provides userId; system generates reset token.
// Never logs token values. Validated on shape (required userId) at Controller boundary;
// user existence and token generation occurs in Business layer (SEC-004, SEC-005).
public class InitiatePasswordResetViewModel
{
    [Required(ErrorMessage = "User ID is required.")]
    public Guid UserId { get; set; }
}
