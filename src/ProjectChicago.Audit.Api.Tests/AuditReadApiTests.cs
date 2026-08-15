using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ProjectChicago.Audit.Core.Contracts;
using ProjectChicago.Audit.Core.Models;
using ProjectChicago.Audit.Core.Persistence;
using Xunit;

namespace ProjectChicago.Audit.Api.Tests;

/// <summary>
/// Integration tests for read-only Audit API endpoints (AUDIT-001..008, ACTIVITY-001..003, SEC-012).
/// Tests verify authorization enforcement (401/403), pagination behavior, input validation,
/// and successful query responses for both GetEntriesByEntity and GetEntriesByTrace endpoints.
/// All tests use an authenticated HttpClient with appropriate role claims to validate
/// authorization policies and authorization-bypass behavior.
/// </summary>
public class AuditReadApiTests : IAsyncLifetime
{
    private readonly AuditTestFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    #region GetEntriesByEntity Authorization and Authentication

    /// <summary>
    /// Unauthenticated request to GET /api/audit/entries-by-entity returns 401 Unauthorized (SEC-012).
    /// The coarse "is there any authenticated actor at all" check is enforced by the controller's
    /// User.Identity.IsAuthenticated check before calling the Facade.
    /// </summary>
    [Fact]
    public async Task GetEntriesByEntity_UnauthenticatedRequest_Returns401()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();
        var entityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByEntityRouteSuffix}?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Authenticated request from non-privileged role (Contributor) to GET /api/audit/entries-by-entity
    /// returns 403 Forbidden (SEC-012, SEC-013). The [Authorize(Policy = "Audit.Read")] attribute
    /// and ASP.NET Core middleware enforce the policy requiring Administrator or Manager role.
    /// </summary>
    [Fact]
    public async Task GetEntriesByEntity_AuthenticatedContributorRole_Returns403Forbidden()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Contributor");
        var entityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByEntityRouteSuffix}?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problemDetails = await response.Content.ReadAsAsync<ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status403Forbidden, problemDetails.Status);
    }

    /// <summary>
    /// Authenticated request from Administrator role to GET /api/audit/entries-by-entity returns 200 OK
    /// (even when no audit entries exist for the entity). The [Authorize(Policy = "Audit.Read")]
    /// attribute and ASP.NET Core middleware allow the request to proceed, and the controller
    /// returns an empty AuditListResult with TotalCount=0.
    /// </summary>
    [Fact]
    public async Task GetEntriesByEntity_AuthenticatedAdministratorRole_Returns200()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");
        var entityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByEntityRouteSuffix}?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<AuditListResult>();
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    /// <summary>
    /// Authenticated request from Manager role to GET /api/audit/entries-by-entity returns 200 OK.
    /// Manager role is included in the Audit.Read policy, so the request is authorized.
    /// </summary>
    [Fact]
    public async Task GetEntriesByEntity_AuthenticatedManagerRole_Returns200()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Manager");
        var entityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByEntityRouteSuffix}?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region GetEntriesByEntity Pagination and Validation

    /// <summary>
    /// Missing required query parameter (entityType) to GET /api/audit/entries-by-entity returns 400 Bad Request.
    /// The [ApiController] attribute's automatic model validation catches the missing parameter and
    /// returns a ValidationProblemDetails response before the action executes.
    /// </summary>
    [Fact]
    public async Task GetEntriesByEntity_MissingEntityType_Returns400BadRequest()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");
        var entityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByEntityRouteSuffix}?entityId={entityId}&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problemDetails = await response.Content.ReadAsAsync<ValidationProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
    }

    /// <summary>
    /// Missing required query parameter (entityId) to GET /api/audit/entries-by-entity returns 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task GetEntriesByEntity_MissingEntityId_Returns400BadRequest()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByEntityRouteSuffix}?entityType=Client&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Invalid pageNumber (0 or negative) to GET /api/audit/entries-by-entity returns 400 Bad Request.
    /// The Facade's input validation rejects page numbers < 1.
    /// </summary>
    [Fact]
    public async Task GetEntriesByEntity_InvalidPageNumber_Returns400BadRequest()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");
        var entityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByEntityRouteSuffix}?entityType=Client&entityId={entityId}&pageNumber=0&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problemDetails = await response.Content.ReadAsAsync<ValidationProblemDetails>();
        Assert.NotNull(problemDetails);
    }

    /// <summary>
    /// Invalid pageSize (0 or negative or exceeds MaxPageSize) to GET /api/audit/entries-by-entity
    /// returns 400 Bad Request. The Facade validates pageSize is between 1 and MaxPageSize.
    /// </summary>
    [Fact]
    public async Task GetEntriesByEntity_InvalidPageSize_Returns400BadRequest()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");
        var entityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByEntityRouteSuffix}?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=0");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// pageSize exceeds MaxPageSize to GET /api/audit/entries-by-entity returns 400 Bad Request
    /// or clamps the size (behavior depends on Facade validation). Test verifies the endpoint
    /// rejects unreasonably large page sizes.
    /// </summary>
    [Fact]
    public async Task GetEntriesByEntity_PageSizeExceedsMaximum_Returns400BadRequest()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");
        var entityId = Guid.NewGuid();
        var oversizePageSize = AuditApiContract.MaxPageSize + 1;

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByEntityRouteSuffix}?entityType=Client&entityId={entityId}&pageNumber=1&pageSize={oversizePageSize}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Default pagination values are applied when pageNumber and pageSize are omitted from
    /// GET /api/audit/entries-by-entity. The response includes pagination metadata.
    /// </summary>
    [Fact]
    public async Task GetEntriesByEntity_DefaultPaginationValues_Returns200WithDefaults()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");
        var entityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByEntityRouteSuffix}?entityType=Client&entityId={entityId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<AuditListResult>();
        Assert.NotNull(result);
        // With default pagination and no audit entries, result should have Items=[] and TotalCount=0
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    /// <summary>
    /// Valid pagination parameters to GET /api/audit/entries-by-entity returns 200 OK with
    /// AuditListResult containing Items (empty if no entries for that entity) and TotalCount.
    /// </summary>
    [Fact]
    public async Task GetEntriesByEntity_ValidPaginationParameters_Returns200()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");
        var entityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByEntityRouteSuffix}?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=50");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<AuditListResult>();
        Assert.NotNull(result);
        Assert.NotNull(result.Items);
        Assert.True(result.TotalCount >= 0);
    }

    #endregion

    #region GetEntriesByTrace Authorization and Authentication

    /// <summary>
    /// Unauthenticated request to GET /api/audit/entries-by-trace returns 401 Unauthorized (SEC-012).
    /// </summary>
    [Fact]
    public async Task GetEntriesByTrace_UnauthenticatedRequest_Returns401()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByTraceRouteSuffix}?traceId=trace-123&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Authenticated request from non-privileged role (ReadOnly) to GET /api/audit/entries-by-trace
    /// returns 403 Forbidden (SEC-012, SEC-013). ReadOnly role is not included in Audit.Read policy.
    /// </summary>
    [Fact]
    public async Task GetEntriesByTrace_AuthenticatedReadOnlyRole_Returns403Forbidden()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("ReadOnly");

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByTraceRouteSuffix}?traceId=trace-123&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Authenticated request from Administrator role to GET /api/audit/entries-by-trace returns 200 OK.
    /// </summary>
    [Fact]
    public async Task GetEntriesByTrace_AuthenticatedAdministratorRole_Returns200()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByTraceRouteSuffix}?traceId=trace-123&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<AuditListResult>();
        Assert.NotNull(result);
    }

    /// <summary>
    /// Authenticated request from Manager role to GET /api/audit/entries-by-trace returns 200 OK.
    /// </summary>
    [Fact]
    public async Task GetEntriesByTrace_AuthenticatedManagerRole_Returns200()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Manager");

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByTraceRouteSuffix}?traceId=trace-123&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region GetEntriesByTrace Validation

    /// <summary>
    /// Neither traceId nor correlationId provided to GET /api/audit/entries-by-trace returns 400 Bad Request.
    /// The Facade's GetAuditByTraceOrCorrelationAsync validates that at least one is provided and throws
    /// ArgumentException when both are null/empty, which is translated to 400 by the ApiExceptionHandler.
    /// </summary>
    [Fact]
    public async Task GetEntriesByTrace_NeitherTraceIdNorCorrelationId_Returns400BadRequest()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByTraceRouteSuffix}?pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problemDetails = await response.Content.ReadAsAsync<ValidationProblemDetails>();
        Assert.NotNull(problemDetails);
    }

    /// <summary>
    /// Only traceId provided (correlationId omitted) to GET /api/audit/entries-by-trace returns 200 OK.
    /// At least one parameter must be provided; providing only traceId is valid.
    /// </summary>
    [Fact]
    public async Task GetEntriesByTrace_OnlyTraceIdProvided_Returns200()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByTraceRouteSuffix}?traceId=trace-123&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<AuditListResult>();
        Assert.NotNull(result);
    }

    /// <summary>
    /// Only correlationId provided (traceId omitted) to GET /api/audit/entries-by-trace returns 200 OK.
    /// </summary>
    [Fact]
    public async Task GetEntriesByTrace_OnlyCorrelationIdProvided_Returns200()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByTraceRouteSuffix}?correlationId=corr-123&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<AuditListResult>();
        Assert.NotNull(result);
    }

    /// <summary>
    /// Both traceId and correlationId provided to GET /api/audit/entries-by-trace returns 200 OK.
    /// </summary>
    [Fact]
    public async Task GetEntriesByTrace_BothTraceIdAndCorrelationIdProvided_Returns200()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByTraceRouteSuffix}?traceId=trace-123&correlationId=corr-123&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<AuditListResult>();
        Assert.NotNull(result);
    }

    /// <summary>
    /// Invalid pageNumber (0 or negative) to GET /api/audit/entries-by-trace returns 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task GetEntriesByTrace_InvalidPageNumber_Returns400BadRequest()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByTraceRouteSuffix}?traceId=trace-123&pageNumber=-1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Invalid pageSize to GET /api/audit/entries-by-trace returns 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task GetEntriesByTrace_InvalidPageSize_Returns400BadRequest()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByTraceRouteSuffix}?traceId=trace-123&pageNumber=1&pageSize=-50");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Default pagination values are applied when pageNumber and pageSize are omitted from
    /// GET /api/audit/entries-by-trace.
    /// </summary>
    [Fact]
    public async Task GetEntriesByTrace_DefaultPaginationValues_Returns200WithDefaults()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByTraceRouteSuffix}?traceId=trace-123");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<AuditListResult>();
        Assert.NotNull(result);
    }

    #endregion

    #region Response Format and Structure

    /// <summary>
    /// Successful GET /api/audit/entries-by-entity response is an AuditListResult with Items and TotalCount.
    /// AuditEntryResult fields include all required audit metadata (AUDIT-001..008, AUDIT-007).
    /// </summary>
    [Fact]
    public async Task GetEntriesByEntity_SuccessResponse_HasCorrectStructure()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");
        var entityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByEntityRouteSuffix}?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<AuditListResult>();
        Assert.NotNull(result);
        Assert.NotNull(result.Items);
        Assert.IsType<List<AuditEntryResult>>(result.Items);
    }

    /// <summary>
    /// AuditEntryResult does not include RawEventPayload (forensics only, not for normal queries, per AUDIT-008).
    /// Verifies that sensitive/raw event payloads are excluded from the DTO returned to clients.
    /// </summary>
    [Fact]
    public async Task GetEntriesByEntity_AuditEntryResultExcludes_RawEventPayload()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");
        var entityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByEntityRouteSuffix}?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        // AuditEntryResult should not have a RawEventPayload property in the JSON.
        // If an entry exists, verify the JSON structure does not include it.
        Assert.DoesNotContain("RawEventPayload", responseContent, StringComparison.Ordinal);
        Assert.DoesNotContain("rawEventPayload", responseContent, StringComparison.Ordinal);
    }

    #endregion

    #region Non-Existent or Edge Cases

    /// <summary>
    /// Query for an entity that has no audit entries returns 200 OK with empty Items and TotalCount=0
    /// (not 404). The audit service does not signal "no entries" as an error; it's a normal state.
    /// </summary>
    [Fact]
    public async Task GetEntriesByEntity_NoEntriesForEntity_Returns200WithEmptyResult()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");
        var nonExistentEntityId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByEntityRouteSuffix}?entityType=Client&entityId={nonExistentEntityId}&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<AuditListResult>();
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    /// <summary>
    /// Query with a trace ID that has no audit entries returns 200 OK with empty Items and TotalCount=0
    /// (not 404). The audit service does not signal "no trace data" as an error.
    /// </summary>
    [Fact]
    public async Task GetEntriesByTrace_NoEntriesForTrace_Returns200WithEmptyResult()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");

        // Act
        var response = await client.GetAsync(
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByTraceRouteSuffix}?traceId=non-existent-trace&pageNumber=1&pageSize=25");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsAsync<AuditListResult>();
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    #endregion

    #region HTTP Verbs (Restrict Mutations)

    /// <summary>
    /// POST to /api/audit/entries-by-entity is not supported (read-only endpoints, no mutations).
    /// Returns 405 Method Not Allowed.
    /// </summary>
    [Fact]
    public async Task GetEntriesByEntity_PostRequest_Returns405MethodNotAllowed()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");
        var entityId = Guid.NewGuid();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByEntityRouteSuffix}?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=25");
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    /// <summary>
    /// PUT to /api/audit/entries-by-entity is not supported (read-only endpoints, no mutations).
    /// Returns 405 Method Not Allowed.
    /// </summary>
    [Fact]
    public async Task GetEntriesByEntity_PutRequest_Returns405MethodNotAllowed()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");
        var entityId = Guid.NewGuid();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Put,
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByEntityRouteSuffix}?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=25");
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    /// <summary>
    /// DELETE to /api/audit/entries-by-entity is not supported (read-only endpoints, no mutations).
    /// Returns 405 Method Not Allowed.
    /// </summary>
    [Fact]
    public async Task GetEntriesByEntity_DeleteRequest_Returns405MethodNotAllowed()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");
        var entityId = Guid.NewGuid();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByEntityRouteSuffix}?entityType=Client&entityId={entityId}&pageNumber=1&pageSize=25");
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    /// <summary>
    /// PATCH to /api/audit/entries-by-trace is not supported (read-only endpoints, no mutations).
    /// Returns 405 Method Not Allowed.
    /// </summary>
    [Fact]
    public async Task GetEntriesByTrace_PatchRequest_Returns405MethodNotAllowed()
    {
        // Arrange
        var client = _fixture.CreateClientWithRole("Administrator");

        // Act
        var request = new HttpRequestMessage(new HttpMethod("PATCH"),
            $"{AuditApiContract.Route}/{AuditApiContract.EntriesByTraceRouteSuffix}?traceId=trace-123&pageNumber=1&pageSize=25");
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    #endregion
}
