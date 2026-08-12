using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectChicago.ServiceDefaults.Errors;
using Xunit;

namespace ProjectChicago.ServiceDefaults.Tests.Errors;

// ERROR-001/TRACE-005: proves status-code-only Problem Details responses (no exception, e.g. an
// unmatched route or a future automatic 400) end up in the same safe, traceable shape as
// exception-driven failures from ApiExceptionHandler.
public class ApiProblemDetailsCustomizerTests
{
    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, "validation_failed")]
    [InlineData(StatusCodes.Status401Unauthorized, "authentication_required")]
    [InlineData(StatusCodes.Status403Forbidden, "forbidden")]
    [InlineData(StatusCodes.Status404NotFound, "resource_not_found")]
    [InlineData(StatusCodes.Status409Conflict, "concurrency_conflict")]
    [InlineData(StatusCodes.Status500InternalServerError, "internal_error")]
    public void Customize_KnownStatus_SetsExpectedErrorCode(int status, string expectedErrorCode)
    {
        var context = CreateContext(status);

        ApiProblemDetailsCustomizer.Customize(context);

        Assert.Equal(expectedErrorCode, context.ProblemDetails.Extensions["errorCode"]);
    }

    [Fact]
    public void Customize_PopulatesTraceIdAndSupportReferenceIdFromRequest()
    {
        var context = CreateContext(StatusCodes.Status404NotFound);
        context.HttpContext.Request.Headers["X-Correlation-Id"] = "correlation-7";

        ApiProblemDetailsCustomizer.Customize(context);

        Assert.Equal("correlation-7", context.ProblemDetails.Extensions["supportReferenceId"]);
        Assert.False(string.IsNullOrWhiteSpace((string?)context.ProblemDetails.Extensions["traceId"]));
    }

    [Fact]
    public void Customize_ErrorCodeAlreadySet_IsNotOverwritten()
    {
        var context = CreateContext(StatusCodes.Status404NotFound);
        context.ProblemDetails.Extensions["errorCode"] = "resource_not_found_custom";

        ApiProblemDetailsCustomizer.Customize(context);

        Assert.Equal("resource_not_found_custom", context.ProblemDetails.Extensions["errorCode"]);
    }

    private static ProblemDetailsContext CreateContext(int status) =>
        new()
        {
            HttpContext = new DefaultHttpContext(),
            ProblemDetails = new ProblemDetails { Status = status },
        };
}
