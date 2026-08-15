using System.ComponentModel.DataAnnotations;
using ProjectChicago.Identity.Core.Authorization.Business;
using ProjectChicago.Identity.Core.Authorization.Contracts;
using ProjectChicago.Identity.Core.Authorization.Data;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Identity.Core.Authorization.Facade;

// User management use-case orchestration (add-endpoint: facade layer, SEC-004, SEC-010..016, AUDIT-001).
// Validates ViewModel shape, resolves request context for audit correlation, delegates to Business,
// and records audit events through Data layer. Does not map ViewModel/ServiceModel fields; Business
// owns the contract translation (SEC-002, SEC-003, SEC-004, identity.md, AUDIT-001..008, OUTBOX-001..006).
public class UserManagementFacade
{
    private readonly UserManagementBusiness _business;
    private readonly UserManagementData _data;
    private readonly ICurrentRequestContext _requestContext;

    public UserManagementFacade(
        UserManagementBusiness business,
        UserManagementData data,
        ICurrentRequestContext requestContext)
    {
        ArgumentNullException.ThrowIfNull(business);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(requestContext);
        _business = business;
        _data = data;
        _requestContext = requestContext;
    }

    // Create a new user with assigned role (SEC-004, SEC-010..016, identity.md, AUDIT-001..008).
    // Validates ViewModel shape, delegates to Business for password/role validation,
    // records audit event through Data, returns ServiceModel without credentials.
    public async Task<UserServiceModel> CreateUserAsync(CreateUserViewModel request, CancellationToken cancellationToken = default)
    {
        // Transport validation catches shape/format issues (required fields, lengths, format).
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true))
        {
            var errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
            throw new ArgumentException($"User creation request validation failed: {errors}");
        }

        // Delegate to Business for password policy, role existence, and duplicate detection
        var serviceModel = await _business.CreateUserAsync(request, cancellationToken).ConfigureAwait(false);

        // Record user creation audit event (SEC-004, AUDIT-001..008).
        // Must find the created user to record the audit event correctly.
        // Note: In this flow, we trust Business created the user successfully.
        // In production, a separate read from UserManager may be required for complete audit context;
        // here we construct the minimal audit event from the returned ServiceModel.
        if (serviceModel.UserId != Guid.Empty)
        {
            // For audit purposes, we need the ApplicationUser entity. Since Business just created it,
            // we would need to either:
            // 1. Have Business return the full user entity alongside the ServiceModel
            // 2. Query it again (violates facade isolation)
            // 3. Pass a delegate to record audit within Business transaction
            // For now, we'll accept the limitation that audit is recorded after Business completes,
            // which is acceptable since the Data layer creates its own transaction for the outbox write.
            // In a production system with stricter requirements, Business would return both
            // the ServiceModel and a reference allowing the Facade to record the audit with full context.

            // As a workaround for this specific implementation, the audit recording can occur
            // without needing the full ApplicationUser object - we only need the UserId and role name.
            // We construct a minimal entity for audit purposes.
            var auditUser = new ProjectChicago.Identity.Core.Models.DataModels.Entities.ApplicationUser
            {
                Id = serviceModel.UserId,
                Email = serviceModel.Email,
            };

            await _data.RecordUserCreatedAsync(auditUser, serviceModel.RoleName, _requestContext.Current, cancellationToken).ConfigureAwait(false);
        }

        return serviceModel;
    }

    // Deactivate a user account (SEC-004, SEC-010..016, identity.md, AUDIT-001..008).
    // Prevents future authentication and invalidates existing sessions. Records audit event.
    public async Task<UserServiceModel> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Delegate to Business for lockout/SecurityStamp update
        var serviceModel = await _business.DeactivateUserAsync(userId, cancellationToken).ConfigureAwait(false);

        // Record user deactivation audit event
        var auditUser = new ProjectChicago.Identity.Core.Models.DataModels.Entities.ApplicationUser
        {
            Id = serviceModel.UserId,
            Email = serviceModel.Email,
        };

        await _data.RecordUserDeactivatedAsync(auditUser, _requestContext.Current, cancellationToken).ConfigureAwait(false);

        return serviceModel;
    }

    // Activate a user account (SEC-004, SEC-010..016, identity.md, AUDIT-001..008).
    // Restores eligibility for authentication. Records audit event.
    public async Task<UserServiceModel> ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Delegate to Business for lockout removal/SecurityStamp update
        var serviceModel = await _business.ActivateUserAsync(userId, cancellationToken).ConfigureAwait(false);

        // Record user activation audit event
        var auditUser = new ProjectChicago.Identity.Core.Models.DataModels.Entities.ApplicationUser
        {
            Id = serviceModel.UserId,
            Email = serviceModel.Email,
        };

        await _data.RecordUserActivatedAsync(auditUser, _requestContext.Current, cancellationToken).ConfigureAwait(false);

        return serviceModel;
    }

    // Add role to user (SEC-004, SEC-010..016, identity.md, AUDIT-001..008).
    // Validates role and user, adds role, records audit event.
    public async Task<UserServiceModel> AddRoleAsync(Guid userId, AddRoleViewModel request, CancellationToken cancellationToken = default)
    {
        // Transport validation catches shape/format issues
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true))
        {
            var errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
            throw new ArgumentException($"Add role request validation failed: {errors}");
        }

        // Delegate to Business for role validation and assignment
        var serviceModel = await _business.AddRoleAsync(userId, request.RoleName, cancellationToken).ConfigureAwait(false);

        // Record role added audit event
        var auditUser = new ProjectChicago.Identity.Core.Models.DataModels.Entities.ApplicationUser
        {
            Id = serviceModel.UserId,
            Email = serviceModel.Email,
        };

        await _data.RecordRoleAddedAsync(auditUser, request.RoleName, _requestContext.Current, cancellationToken).ConfigureAwait(false);

        return serviceModel;
    }

    // Remove role from user (SEC-004, SEC-010..016, identity.md, AUDIT-001..008).
    // Validates role and user, removes role, records audit event.
    public async Task<UserServiceModel> RemoveRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        // Delegate to Business for role removal
        var serviceModel = await _business.RemoveRoleAsync(userId, roleName, cancellationToken).ConfigureAwait(false);

        // Record role removed audit event
        var auditUser = new ProjectChicago.Identity.Core.Models.DataModels.Entities.ApplicationUser
        {
            Id = serviceModel.UserId,
            Email = serviceModel.Email,
        };

        await _data.RecordRoleRemovedAsync(auditUser, roleName, _requestContext.Current, cancellationToken).ConfigureAwait(false);

        return serviceModel;
    }

    // Change password for authenticated user (SEC-004, SEC-005, identity.md, AUDIT-001..008).
    // Validates ViewModel shape, delegates to Business for password change, records audit event.
    public async Task<UserServiceModel> ChangePasswordAsync(Guid userId, ChangePasswordViewModel request, CancellationToken cancellationToken = default)
    {
        // Transport validation catches shape/format issues (required fields, lengths, match)
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true))
        {
            var errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
            throw new ArgumentException($"Change password request validation failed: {errors}");
        }

        // Delegate to Business for password change (validates current password, enforces policy)
        var serviceModel = await _business.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken).ConfigureAwait(false);

        // Record password change audit event (SEC-005: auditable, no password value)
        var auditUser = new ProjectChicago.Identity.Core.Models.DataModels.Entities.ApplicationUser
        {
            Id = serviceModel.UserId,
            Email = serviceModel.Email,
        };

        await _data.RecordPasswordChangedAsync(auditUser, _requestContext.Current, cancellationToken).ConfigureAwait(false);

        return serviceModel;
    }

    // Initiate password reset for user (admin-only, SEC-004, SEC-005, identity.md, AUDIT-001..008).
    // Generates a one-time reset token and records audit event. Admin communicates token to user.
    public async Task<string> InitiatePasswordResetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Delegate to Business to generate reset token
        var token = await _business.InitiatePasswordResetAsync(userId, cancellationToken).ConfigureAwait(false);

        // Record password reset initiation audit event (SEC-005: auditable, no token value)
        var user = new ProjectChicago.Identity.Core.Models.DataModels.Entities.ApplicationUser
        {
            Id = userId,
        };

        await _data.RecordPasswordResetInitiatedAsync(user, _requestContext.Current, cancellationToken).ConfigureAwait(false);

        return token;
    }

    // Complete password reset for user (SEC-004, SEC-005, identity.md, AUDIT-001..008).
    // Validates reset token ViewModel shape, delegates to Business for reset, records audit event.
    public async Task<UserServiceModel> ResetPasswordAsync(Guid userId, ResetPasswordViewModel request, CancellationToken cancellationToken = default)
    {
        // Transport validation catches shape/format issues (required fields, lengths, match)
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true))
        {
            var errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
            throw new ArgumentException($"Reset password request validation failed: {errors}");
        }

        // Delegate to Business for password reset (validates token, enforces policy)
        var serviceModel = await _business.ResetPasswordAsync(userId, request.Token, request.NewPassword, cancellationToken).ConfigureAwait(false);

        // Record password reset audit event (SEC-005: auditable, no token or password value)
        var auditUser = new ProjectChicago.Identity.Core.Models.DataModels.Entities.ApplicationUser
        {
            Id = serviceModel.UserId,
            Email = serviceModel.Email,
        };

        await _data.RecordPasswordResetAsync(auditUser, _requestContext.Current, cancellationToken).ConfigureAwait(false);

        return serviceModel;
    }

    // List users with pagination (SEC-004, SEC-010..016, identity.md).
    // Administrator-only read operation returning support-safe user metadata.
    // Page/PageSize are validated by [ApiController] automatic model-state validation on the transport contract.
    public async Task<PagedResponse<UserServiceModel>> ListUsersAsync(
        ListUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.PageSize, 1);

        // Delegate to Data for query (read-only, no domain logic)
        var (users, totalCount) = await _data.GetUsersAsync(request.Page, request.PageSize, cancellationToken)
            .ConfigureAwait(false);

        // Compose paginated response
        var totalPages = totalCount == 0 ? 0 : (totalCount + request.PageSize - 1) / request.PageSize;

        return new PagedResponse<UserServiceModel>
        {
            Items = users,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
        };
    }

    // Get a user by ID (SEC-004, SEC-010..016, identity.md).
    // Administrator-only read operation. Returns the user with support-safe metadata,
    // or null if the user does not exist (controller will map this to 404).
    public async Task<UserServiceModel?> GetUserDetailAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId.ToString());

        // Delegate to Data for query (read-only, no domain logic)
        return await _data.GetUserDetailAsync(userId, cancellationToken).ConfigureAwait(false);
    }
}
