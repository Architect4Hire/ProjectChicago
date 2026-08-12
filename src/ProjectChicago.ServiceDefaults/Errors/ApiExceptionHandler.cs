using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectChicago.ServiceDefaults.Correlation;
using ProjectChicago.Shared.Correlation;
using ProjectChicago.Shared.Errors;

namespace ProjectChicago.ServiceDefaults.Errors;

// Terminal ERROR-001..005/LOG-001..006 seam for an ASP.NET Core host: catches whatever exception
// reached the top of the pipeline unhandled, classifies it into the ApiProblemDetailsFactory safe
// shape, logs it exactly once at this boundary (LOG-006), and never lets exception/stack-trace/SQL
// detail reach the client (ERROR-002). Only well-known BCL exception types are classified into a
// specific ERROR-003 category below; a service must not grow this switch with bespoke business
// exception types (backend.md) - domain/data-specific translation belongs to the owning Data layer
// (onion-boundaries.md). Registered per host via AddApiExceptionHandling (Program.cs).
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IHostEnvironment environment)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var requestContext = HttpRequestContextFactory.Create(httpContext);
        var problem = Classify(exception, requestContext);

        Log(exception, requestContext, problem, httpContext);

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        var problemDetailsService = httpContext.RequestServices.GetService<IProblemDetailsService>();
        if (problemDetailsService is not null)
        {
            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = problem,
            });
        }

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static ProblemDetails Classify(Exception exception, RequestContext requestContext) =>
        exception switch
        {
            System.ComponentModel.DataAnnotations.ValidationException => ApiProblemDetailsFactory.Validation(requestContext),
            KeyNotFoundException => ApiProblemDetailsFactory.NotFound(requestContext),
            UnauthorizedAccessException => ApiProblemDetailsFactory.Forbidden(requestContext),
            _ => ApiProblemDetailsFactory.InternalError(requestContext),
        };

    private void Log(Exception exception, RequestContext requestContext, ProblemDetails problem, HttpContext httpContext)
    {
        var operation = httpContext.GetEndpoint()?.DisplayName
            ?? $"{httpContext.Request.Method} {httpContext.Request.Path}";
        var isUnexpected = (problem.Status ?? StatusCodes.Status500InternalServerError) >= StatusCodes.Status500InternalServerError;

        // LOG-005: exception type/message/stack trace come from passing `exception` as the ILogger
        // exception argument; TraceId/Service/Operation are structured fields (LOG-001/LOG-002).
        logger.Log(
            isUnexpected ? LogLevel.Error : LogLevel.Warning,
            exception,
            "Request failed with {ErrorCode}. TraceId={TraceId} Service={Service} Operation={Operation}",
            problem.Extensions[ApiProblemDetailsExtensions.ErrorCode],
            requestContext.TraceId,
            environment.ApplicationName,
            operation);
    }
}
