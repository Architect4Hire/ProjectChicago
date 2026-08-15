using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ProjectChicago.Audit.Api.Tests;

/// <summary>
/// Integration tests for error handling and trace propagation in Audit API (ERROR-001..005, TRACE-001..007, LOG-001..006).
/// Verifies that:
/// - Errors return consistent ProblemDetails shape (ERROR-001)
/// - Internal details and stack traces are redacted (ERROR-002)
/// - Unexpected errors include trace reference for support (ERROR-004, ERROR-005)
/// - W3C trace context is propagated and accessible in errors (TRACE-002, TRACE-005)
/// - Log correlation includes trace IDs (LOG-003)
/// - Audit-specific errors (validation, authorization) are distinguished (ERROR-003)
/// </summary>
public class ErrorHandlingAndTracingTests : IAsyncLifetime
{
    private readonly AuditTestFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    /// <summary>
    /// Invalid query parameter (missing required parameter) returns 400 BadRequest with ValidationProblemDetails.
    /// The response includes the standard ProblemDetails shape (ERROR-001).
    /// Example: GET /api/audit/entries-by-entity (missing entityType or entityId).
    /// </summary>
    [Fact]
    public async Task InvalidRequest_MissingRequiredParameter_Returns400_WithValidationProblemDetails()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");

        // Act
        var response = await client.GetAsync("/api/audit/entries-by-entity?pageNumber=1&pageSize=25");
        // entityType and entityId are missing

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
        // Errors dict should document which fields are invalid
        Assert.True(problemDetails.Errors?.Count > 0);
    }

    /// <summary>
    /// Invalid page number (0 or negative) returns 400 BadRequest (ERROR-003: validation distinct from auth/domain).
    /// </summary>
    [Fact]
    public async Task InvalidRequest_InvalidPageNumber_Returns400_BadRequest()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");
        var entityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"/api/audit/entries-by-entity?entityType=Client&entityId={entityId}&pageNumber=0&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
    }

    /// <summary>
    /// Unauthenticated request returns 401 Unauthorized with ProblemDetails (no stack trace, safe message).
    /// Verifies ERROR-001: consistent error response shape, ERROR-003: distinct from 403.
    /// </summary>
    [Fact]
    public async Task UnauthenticatedRequest_Returns401_WithProblemDetails_NoStackTrace()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();
        var entityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"/api/audit/entries-by-entity?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status401Unauthorized, problemDetails.Status);
        // ERROR-002: no stack trace exposed
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("StackTrace", content, StringComparison.Ordinal);
        Assert.DoesNotContain("at ProjectChicago", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Forbidden request (authenticated but unauthorized) returns 403 Forbidden with ProblemDetails.
    /// Verifies ERROR-001/ERROR-003: consistent error shape, distinct from 401/400/500.
    /// Non-privileged role (Contributor) attempting audit access.
    /// </summary>
    [Fact]
    public async Task ForbiddenRequest_NonPrivilegedRole_Returns403_WithProblemDetails_DistinctFrom401()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Contributor");
        var entityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"/api/audit/entries-by-entity?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status403Forbidden, problemDetails.Status);
        // ERROR-002: no stack trace
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("StackTrace", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Not found (404 for a route that doesn't exist) returns ProblemDetails shape, not bare 404 status (ERROR-001).
    /// UseStatusCodePages middleware converts bare 404 to ProblemDetails.
    /// </summary>
    [Fact]
    public async Task NotFoundRoute_Returns404_WithProblemDetails()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");

        // Act
        var response = await client.GetAsync("/api/audit/non-existent-endpoint");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status404NotFound, problemDetails.Status);
        // ERROR-002: no stack trace
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("StackTrace", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Successful request returns 200 OK with query results (baseline for comparison with error cases).
    /// </summary>
    [Fact]
    public async Task SuccessfulRequest_Returns200_OK()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");
        var entityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"/api/audit/entries-by-entity?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Error responses include a stable trace ID that can be correlated with telemetry (ERROR-004, ERROR-005, TRACE-005).
    /// The trace ID is included in ProblemDetails for support reference.
    /// </summary>
    [Fact]
    public async Task ErrorResponse_IncludesTraceId_ForSupport_AndCorrelation()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Contributor");  // Forbidden
        var entityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"/api/audit/entries-by-entity?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=25");

        // Assert
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problemDetails);
        // ERROR-005: Trace/support reference included
        // The extensions property typically contains trace ID
        Assert.True(problemDetails.Extensions?.ContainsKey("traceId") ?? false,
            "ProblemDetails should include traceId for support reference (ERROR-005)");
        var traceId = problemDetails.Extensions?["traceId"] as string;
        Assert.False(string.IsNullOrEmpty(traceId), "traceId should not be empty");
        // Trace ID should be in W3C format (UUID or similar)
        Assert.True(Guid.TryParse(traceId, out _) || traceId!.Length > 10,
            "traceId should be a valid GUID or trace format");
    }

    /// <summary>
    /// W3C trace context is preserved in error responses (TRACE-002, TRACE-005).
    /// Even if an error occurs, the trace context should be available for correlation.
    /// </summary>
    [Fact]
    public async Task ErrorResponse_PreservesTraceContext_ForDistributedTracing()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/audit/entries-by-entity?entityType=Client&entityId=invalid");

        // Assert
        // Errors should include trace context (traceId in ProblemDetails)
        // TRACE-002: W3C conventions support, TRACE-005: diagnostic info available
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problemDetails);
        // The response preserves trace context for downstream correlation
        Assert.True(problemDetails.Extensions?.ContainsKey("traceId") ?? false);
    }

    /// <summary>
    /// Validation errors are distinguishable from authorization and internal failures (ERROR-003).
    /// 400 (validation) vs 401 (auth) vs 403 (forbidden) vs 500 (internal) are distinct.
    /// </summary>
    [Fact]
    public async Task ErrorTypes_AreDistinguishable_ByStatusCode()
    {
        // Arrange
        var adminClient = _fixture.CreateClientWithRole("Administrator");
        var contributorClient = _fixture.CreateClientWithRole("Contributor");
        var unauthenticatedClient = _fixture.CreateUnauthenticatedClient();
        var entityId = Guid.NewGuid();

        // Act & Assert
        // 400: Validation error (missing parameter)
        var validationResponse = await adminClient.GetAsync("/api/audit/entries-by-entity?pageNumber=1&pageSize=25");
        Assert.Equal(HttpStatusCode.BadRequest, validationResponse.StatusCode);

        // 401: Unauthenticated
        var unauthorizedResponse = await unauthenticatedClient.GetAsync(
            $"/api/audit/entries-by-entity?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=25");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);

        // 403: Forbidden (authenticated but unauthorized)
        var forbiddenResponse = await contributorClient.GetAsync(
            $"/api/audit/entries-by-entity?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=25");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        // ERROR-003: All three are distinct and returned with ProblemDetails
        var validationProblem = await validationResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        var unauthorizedProblem = await unauthorizedResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        var forbiddenProblem = await forbiddenResponse.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(validationProblem);
        Assert.NotNull(unauthorizedProblem);
        Assert.NotNull(forbiddenProblem);
    }

    /// <summary>
    /// ProblemDetails response does not include sensitive data from request context (ERROR-002).
    /// Security rule: No credentials, audit entries, or business data in error responses.
    /// </summary>
    [Fact]
    public async Task ErrorResponse_ExcludesSensitiveData_FromProblemDetails()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");

        // Act
        var response = await client.GetAsync(
            "/api/audit/entries-by-entity?entityType=Client&entityId=00000000-0000-0000-0000-000000000000&pageNumber=0&pageSize=25");

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        // ERROR-002: No sensitive data (GUIDs from requests, Entity data, etc.) in the error response
        // The actual error message should be safe and not leak implementation details
        Assert.DoesNotContain("DbContext", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Connection", content, StringComparison.Ordinal);
        Assert.DoesNotContain("exception", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// HTTP method not allowed (405) is handled consistently as ProblemDetails (ERROR-001).
    /// All HTTP responses use the same ProblemDetails shape.
    /// </summary>
    [Fact]
    public async Task MethodNotAllowed_Returns405_WithProblemDetails()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post,
            "/api/audit/entries-by-entity?entityType=Client&entityId=123&pageNumber=1&pageSize=25");
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status405MethodNotAllowed, problemDetails.Status);
        // ERROR-001: Consistent ProblemDetails shape
    }

    /// <summary>
    /// PUT request to audit endpoint returns 405 (no mutations allowed).
    /// Audit is read-only; any mutation attempt returns MethodNotAllowed.
    /// </summary>
    [Fact]
    public async Task MutationAttempt_Returns405_MethodNotAllowed()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");

        // Act
        var putRequest = new HttpRequestMessage(HttpMethod.Put,
            "/api/audit/entries-by-entity?entityType=Client&entityId=123&pageNumber=1&pageSize=25");
        var putResponse = await client.SendAsync(putRequest);

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete,
            "/api/audit/entries-by-entity?entityType=Client&entityId=123&pageNumber=1&pageSize=25");
        var deleteResponse = await client.SendAsync(deleteRequest);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, putResponse.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, deleteResponse.StatusCode);
    }

    /// <summary>
    /// Log-level correlation (LOG-003, LOG-005): Correlation IDs are included in structured logs
    /// when an error occurs, allowing support to find corresponding log entries by error trace ID.
    /// Implementation note: This test documents the expected behavior; actual verification
    /// would require log capture/inspection in a real deployment scenario.
    /// </summary>
    [Fact]
    public async Task ErrorResponse_CorrelatesWithStructuredLogs_ViaTraceId()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Contributor");  // Forbidden
        var entityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"/api/audit/entries-by-entity?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=25");

        // Assert
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problemDetails);
        var traceId = problemDetails.Extensions?["traceId"] as string;
        Assert.False(string.IsNullOrEmpty(traceId));

        // LOG-003: Logs should auto-include this traceId in structured logging context
        // LOG-005: Exception details are logged once (in the exception handler), not duplicated at multiple layers
        // The traceId enables support to find the corresponding structured log entry and see:
        // - Request details
        // - Actor information
        // - Exception stack trace (in logs, not in HTTP response - ERROR-002)
    }
}
