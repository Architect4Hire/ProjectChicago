using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.Shared.Errors;

// Builds the one shared error shape used across every Project Chicago HTTP API (ERROR-001,
// ERROR-003, API-004). Every response distinguishes the ERROR-003 failure categories with a
// stable ApiErrorCodes value, a conventional HTTP status, a title/detail pair drawn only from
// this file's safe, generic copy, and the trace/support reference callers need to escalate
// (ERROR-004, ERROR-005) - never exception, SQL, or Service Bus detail (ERROR-002).
//
// This type only shapes a ProblemDetails instance; it does not catch exceptions, validate
// input, or decide when a failure occurred. Callers (controller/exception-handling composition)
// own that decision and pass in the safe detail/field errors they already determined are safe
// to surface.
public static class ApiProblemDetailsFactory
{
    private const int MaxDetailLength = 300;

    // Returns the concrete ValidationProblemDetails type (not the ProblemDetails base) so its
    // "errors" property/property-to-messages shape survives direct System.Text.Json
    // serialization outside an ASP.NET Core MVC formatter, which otherwise serializes by the
    // caller's declared/static type and would silently drop it (api-contracts.md).
    public static ValidationProblemDetails Validation(
        RequestContext requestContext,
        string? detail = null,
        IReadOnlyDictionary<string, string[]>? fieldErrors = null)
    {
        var problem = new ValidationProblemDetails(
            (fieldErrors ?? new Dictionary<string, string[]>()).ToDictionary(pair => pair.Key, pair => pair.Value));

        Populate(
            problem,
            ApiErrorCodes.Validation,
            StatusCodes.Status400BadRequest,
            "One or more validation errors occurred.",
            "The request contains one or more invalid fields.",
            detail,
            requestContext);

        return problem;
    }

    public static ProblemDetails AuthenticationRequired(RequestContext requestContext, string? detail = null) =>
        Populate(
            new ProblemDetails(),
            ApiErrorCodes.AuthenticationRequired,
            StatusCodes.Status401Unauthorized,
            "Authentication is required.",
            "Sign in is required to access this resource.",
            detail,
            requestContext);

    public static ProblemDetails Forbidden(RequestContext requestContext, string? detail = null) =>
        Populate(
            new ProblemDetails(),
            ApiErrorCodes.Forbidden,
            StatusCodes.Status403Forbidden,
            "You do not have permission to perform this action.",
            "Your account does not have access to this resource.",
            detail,
            requestContext);

    public static ProblemDetails NotFound(RequestContext requestContext, string? detail = null) =>
        Populate(
            new ProblemDetails(),
            ApiErrorCodes.NotFound,
            StatusCodes.Status404NotFound,
            "The requested resource was not found.",
            "The requested resource could not be found.",
            detail,
            requestContext);

    public static ProblemDetails ConcurrencyConflict(RequestContext requestContext, string? detail = null) =>
        Populate(
            new ProblemDetails(),
            ApiErrorCodes.ConcurrencyConflict,
            StatusCodes.Status409Conflict,
            "The resource was changed by another request.",
            "Reload the resource and try again.",
            detail,
            requestContext);

    public static ProblemDetails InternalError(RequestContext requestContext, string? detail = null) =>
        Populate(
            new ProblemDetails(),
            ApiErrorCodes.InternalError,
            StatusCodes.Status500InternalServerError,
            "An unexpected error occurred.",
            "An unexpected error occurred while processing the request. Reference the support ID if you contact support.",
            detail,
            requestContext);

    private static ProblemDetails Populate(
        ProblemDetails problem,
        string errorCode,
        int status,
        string title,
        string defaultDetail,
        string? requestedDetail,
        RequestContext requestContext)
    {
        problem.Status = status;
        problem.Title = title;
        problem.Detail = SanitizeDetail(requestedDetail) ?? defaultDetail;
        problem.Extensions[ApiProblemDetailsExtensions.ErrorCode] = errorCode;
        problem.Extensions[ApiProblemDetailsExtensions.TraceId] = requestContext.TraceId;
        problem.Extensions[ApiProblemDetailsExtensions.SupportReferenceId] = requestContext.CorrelationId;
        return problem;
    }

    // Exception messages, SQL errors, and Service Bus fault text are typically long and/or
    // multi-line (stack traces, "at ... in ... :line NN"). Treat anything shaped like that as
    // unsafe and fall back to the fixed, generic detail for the error code instead (ERROR-002).
    private static string? SanitizeDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return null;
        }

        var trimmed = detail.Trim();

        return trimmed.Length <= MaxDetailLength && !ContainsControlCharacter(trimmed)
            ? trimmed
            : null;
    }

    private static bool ContainsControlCharacter(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }
}
