using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjectChicago.ServiceDefaults.Errors;
using Xunit;

namespace ProjectChicago.ServiceDefaults.Tests.Errors;

// ERROR-001..005/LOG-001..006/TRACE-005: proves the terminal exception -> Problem Details seam
// classifies known BCL exception types into the safe ApiProblemDetailsFactory shape, redacts
// unexpected failures (ERROR-002), always carries a trace/support reference (ERROR-004/005), and
// logs exactly once at this boundary with the required LOG-005 fields.
public class ApiExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ValidationException_MapsTo400WithValidationErrorCode()
    {
        var (handler, _) = CreateHandler();
        var httpContext = CreateHttpContext();

        var handled = await handler.TryHandleAsync(httpContext, new ValidationException("email is required"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.Equal("validation_failed", await ReadErrorCodeAsync(httpContext));
    }

    [Fact]
    public async Task TryHandleAsync_KeyNotFoundException_MapsTo404WithNotFoundErrorCode()
    {
        var (handler, _) = CreateHandler();
        var httpContext = CreateHttpContext();

        var handled = await handler.TryHandleAsync(httpContext, new KeyNotFoundException("client 42 not found"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.Equal("resource_not_found", await ReadErrorCodeAsync(httpContext));
    }

    [Fact]
    public async Task TryHandleAsync_UnauthorizedAccessException_MapsTo403WithForbiddenErrorCode()
    {
        var (handler, _) = CreateHandler();
        var httpContext = CreateHttpContext();

        var handled = await handler.TryHandleAsync(httpContext, new UnauthorizedAccessException(), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Equal("forbidden", await ReadErrorCodeAsync(httpContext));
    }

    [Fact]
    public async Task TryHandleAsync_UnknownException_MapsTo500WithSafeGenericDetail_NoExceptionDetailLeaked()
    {
        var (handler, _) = CreateHandler();
        var httpContext = CreateHttpContext();
        var exception = new InvalidOperationException(
            "System.Data.SqlClient.SqlException: connection to SQL Server 'sql-1' failed, see inner exception for the driver-reported diagnostic code");

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        var body = await ReadBodyAsync(httpContext);
        Assert.Equal("internal_error", JsonDocument.Parse(body).RootElement.GetProperty("errorCode").GetString());
        Assert.DoesNotContain("driver-reported diagnostic code", body);
        Assert.DoesNotContain("SqlException", body);
    }

    [Fact]
    public async Task TryHandleAsync_AnyException_PopulatesTraceIdAndSupportReferenceIdFromRequest()
    {
        var (handler, _) = CreateHandler();
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers["X-Correlation-Id"] = "correlation-42";

        await handler.TryHandleAsync(httpContext, new KeyNotFoundException(), CancellationToken.None);

        var body = await ReadBodyAsync(httpContext);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal("correlation-42", root.GetProperty("supportReferenceId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task TryHandleAsync_UnexpectedException_LogsOnceAtErrorWithExceptionAndTraceId()
    {
        var (handler, logger) = CreateHandler();
        var httpContext = CreateHttpContext();
        var exception = new InvalidOperationException("boom");

        await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public async Task TryHandleAsync_ExpectedFailure_LogsAtWarningNotError()
    {
        var (handler, logger) = CreateHandler();
        var httpContext = CreateHttpContext();

        await handler.TryHandleAsync(httpContext, new KeyNotFoundException(), CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
    }

    private static (ApiExceptionHandler Handler, RecordingLogger<ApiExceptionHandler> Logger) CreateHandler()
    {
        var logger = new RecordingLogger<ApiExceptionHandler>();
        var handler = new ApiExceptionHandler(logger, new FakeHostEnvironment());
        return (handler, logger);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
            Response = { Body = new MemoryStream() },
        };
        return httpContext;
    }

    private static async Task<string> ReadBodyAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpContext httpContext)
    {
        var body = await ReadBodyAsync(httpContext);
        return JsonDocument.Parse(body).RootElement.GetProperty("errorCode").GetString();
    }
}
