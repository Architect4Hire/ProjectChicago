using System.ComponentModel.DataAnnotations;
using ProjectChicago.Identity.Core.Authorization.Business;
using ProjectChicago.Identity.Core.Authorization.Contracts;
using ProjectChicago.Identity.Core.Authorization.Data;
using ProjectChicago.Identity.Core.Models.DataModels.Entities;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Identity.Core.Authorization.Facade;

// Authentication use-case orchestration (add-endpoint: facade layer, AUDIT-001). Validates ViewModel,
// resolves session expiration and request context, delegates to Business, and records audit events through
// Data layer based on login outcome. Does not map ViewModel/ServiceModel fields; Business owns the contract
// translation. (SEC-001, SEC-020..025, ADR-0018, AUDIT-001..008, OUTBOX-001..006).
public class AuthenticationFacade
{
    private readonly AuthenticationBusiness _business;
    private readonly AuthenticationData _data;
    private readonly ICurrentRequestContext _requestContext;
    private readonly TimeSpan _sessionLifetime = TimeSpan.FromMinutes(30);

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

        // Compute session expiration for response. Client uses this to display session timeout UI.
        var expiresAtUtc = DateTime.UtcNow.Add(_sessionLifetime);

        var loginResult = await _business.LoginAsync(request, expiresAtUtc, cancellationToken).ConfigureAwait(false);

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

    // Record logout audit event (SEC-005, AUDIT-001).
    public async Task RecordLogoutAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await _data.RecordLogoutAsync(user, _requestContext.Current, cancellationToken).ConfigureAwait(false);
    }
}
