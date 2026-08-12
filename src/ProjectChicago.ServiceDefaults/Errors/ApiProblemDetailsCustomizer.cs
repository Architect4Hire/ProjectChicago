using Microsoft.AspNetCore.Http;
using ProjectChicago.ServiceDefaults.Correlation;
using ProjectChicago.Shared.Errors;

namespace ProjectChicago.ServiceDefaults.Errors;

// Keeps status-code-only Problem Details responses (no exception involved - e.g. an unmatched
// route, or a future [ApiController] automatic 400) in the same ERROR-001/TRACE-005 shape that
// ApiExceptionHandler produces for exception-driven failures, so every response the host returns
// carries a traceId/supportReferenceId/errorCode regardless of which ASP.NET Core path produced it.
public static class ApiProblemDetailsCustomizer
{
    public static void Customize(ProblemDetailsContext context)
    {
        var requestContext = HttpRequestContextFactory.Create(context.HttpContext);
        var problem = context.ProblemDetails;

        problem.Extensions[ApiProblemDetailsExtensions.TraceId] = requestContext.TraceId;
        problem.Extensions[ApiProblemDetailsExtensions.SupportReferenceId] = requestContext.CorrelationId;
        problem.Extensions.TryAdd(ApiProblemDetailsExtensions.ErrorCode, ResolveErrorCode(problem.Status));
    }

    private static string ResolveErrorCode(int? status) => status switch
    {
        StatusCodes.Status400BadRequest => ApiErrorCodes.Validation,
        StatusCodes.Status401Unauthorized => ApiErrorCodes.AuthenticationRequired,
        StatusCodes.Status403Forbidden => ApiErrorCodes.Forbidden,
        StatusCodes.Status404NotFound => ApiErrorCodes.NotFound,
        StatusCodes.Status409Conflict => ApiErrorCodes.ConcurrencyConflict,
        _ => ApiErrorCodes.InternalError,
    };
}
