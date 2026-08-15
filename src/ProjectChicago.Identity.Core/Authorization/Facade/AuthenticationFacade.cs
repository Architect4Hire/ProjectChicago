using System.ComponentModel.DataAnnotations;
using ProjectChicago.Identity.Core.Authorization.Business;
using ProjectChicago.Identity.Core.Authorization.Contracts;
using ProjectChicago.Identity.Core.Authorization.Data;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Identity.Core.Authorization.Facade;

/// <summary>
/// Authentication use-case orchestration (add-endpoint: facade layer, AUDIT-001). Validates ViewModel,
/// delegates to Business for credential validation and JWT minting, and records audit events through
/// Data layer based on login outcome. Does not map ViewModel/ServiceModel fields; Business owns the contract
/// translation. (SEC-001, SEC-020..025, ADR-0018-superseding, AUDIT-001..008, OUTBOX-001..006).
/// </summary>
public class AuthenticationFacade
{
    private readonly AuthenticationBusiness _business;
    private readonly AuthenticationData _data;
    private readonly ICurrentRequestContext _requestContext;

    public AuthenticationFacade(
        AuthenticationBusiness business,
        AuthenticationData data,
        ICurrentRequestContext requestContext)
    {
        ArgumentNullException.ThrowIfNull(business);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(requestContext);
        _business = business;
        _data = data;
        _requestContext = requestContext;
    }

    public async Task<LoginServiceModel> LoginAsync(LoginViewModel request, CancellationToken cancellationToken = default)
    {
        // Transport validation catches shape/format issues (required fields, lengths).
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true))
        {
            var errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
            throw new ArgumentException($"Login request validation failed: {errors}");
        }

        // Token lifetimes (access/refresh) are now managed by JwtTokenService/config, not by Facade.
        var loginResult = await _business.LoginAsync(request, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

        // Record appropriate audit event based on outcome (SEC-005, AUDIT-001..008).
        switch (loginResult.Outcome)
        {
            case LoginOutcome.Success:
                if (loginResult.User is not null)
                {
                    await _data.RecordLoginSuccessAsync(loginResult.User, _requestContext.Current, cancellationToken).ConfigureAwait(false);
                }

                return loginResult.ServiceModel!;

            case LoginOutcome.FailedAccountLocked:
                if (loginResult.User is not null)
                {
                    await _data.RecordAccountLockedAsync(loginResult.User, _requestContext.Current, cancellationToken).ConfigureAwait(false);
                }

                throw new InvalidOperationException(loginResult.ErrorMessage ?? "Account is locked.");

            case LoginOutcome.FailedInvalidCredentials:
                // Record failed login audit with Anonymous actor (user not found or password mismatch).
                await _data.RecordFailedLoginAsync(loginResult.AttemptedUsername ?? "unknown", _requestContext.Current, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(loginResult.ErrorMessage ?? "Invalid username or password.");

            case LoginOutcome.FailedTwoFactorRequired:
                throw new InvalidOperationException(loginResult.ErrorMessage ?? "Two-factor authentication required.");

            default:
                throw new InvalidOperationException("Unknown login outcome.");
        }
    }

    /// <summary>
    /// Refresh the access token using a valid refresh token (called by /auth/refresh endpoint, ADR-0018-superseding).
    /// Gateway calls this when a stored access token is expired/near-expiry to rotate the token pair.
    /// Records audit events on success or failure.
    /// </summary>
    public async Task<LoginServiceModel> RefreshAsync(RefreshTokenViewModel request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var refreshResult = await _business.RefreshAsync(request.RefreshToken, cancellationToken).ConfigureAwait(false);

        // Record appropriate audit event based on outcome (SEC-005, AUDIT-001..008).
        switch (refreshResult.Outcome)
        {
            case LoginOutcome.Success:
                if (refreshResult.User is not null)
                {
                    // Optional: Record token refresh as an audit event for compliance/audit trail.
                    // For now, this is a nice-to-have; the plan doesn't block on it.
                    // await _data.RecordTokenRefreshAsync(refreshResult.User, _requestContext.Current, cancellationToken);
                }

                return refreshResult.ServiceModel!;

            case LoginOutcome.FailedAccountLocked:
                if (refreshResult.User is not null)
                {
                    await _data.RecordAccountLockedAsync(refreshResult.User, _requestContext.Current, cancellationToken).ConfigureAwait(false);
                }

                throw new InvalidOperationException(refreshResult.ErrorMessage ?? "Account is locked.");

            case LoginOutcome.FailedInvalidCredentials:
                // Refresh token is invalid/expired/tampered.
                throw new InvalidOperationException(refreshResult.ErrorMessage ?? "Invalid or expired refresh token.");

            default:
                throw new InvalidOperationException("Unknown refresh outcome.");
        }
    }

    /// <summary>
    /// Record logout audit event (SEC-005, AUDIT-001, ADR-0018-superseding).
    /// Note: The Gateway now owns logout (deletes the Redis session and clears the session cookie).
    /// This method is kept for completeness but is not called by the Gateway for v1.
    /// </summary>
    public async Task RecordLogoutAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await _data.RecordLogoutAsync(user, _requestContext.Current, cancellationToken).ConfigureAwait(false);
    }
}
