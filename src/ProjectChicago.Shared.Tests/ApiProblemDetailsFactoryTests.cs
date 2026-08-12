using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectChicago.Shared.Correlation;
using ProjectChicago.Shared.Errors;
using Xunit;

namespace ProjectChicago.Shared.Tests;

public class ApiProblemDetailsFactoryTests
{
    private static readonly RequestContext KnownContext = RequestContext.FromPropagated(
        traceId: "4bf92f3577b34da6a3ce929d0e0e4736",
        correlationId: "correlation-1",
        causationId: null,
        requestId: "request-1",
        actor: ActorContext.ForUser("user-123"));

    public sealed record CategoryCase(
        string Name,
        Func<RequestContext, string?, ProblemDetails> Factory,
        int ExpectedStatus,
        string ExpectedErrorCode,
        string ExpectedDefaultDetail)
    {
        public override string ToString() => Name;
    }

    public static TheoryData<CategoryCase> Categories() =>
    [
        new CategoryCase(
            "Validation",
            (ctx, detail) => ApiProblemDetailsFactory.Validation(ctx, detail),
            StatusCodes.Status400BadRequest,
            ApiErrorCodes.Validation,
            "The request contains one or more invalid fields."),
        new CategoryCase(
            "AuthenticationRequired",
            ApiProblemDetailsFactory.AuthenticationRequired,
            StatusCodes.Status401Unauthorized,
            ApiErrorCodes.AuthenticationRequired,
            "Sign in is required to access this resource."),
        new CategoryCase(
            "Forbidden",
            ApiProblemDetailsFactory.Forbidden,
            StatusCodes.Status403Forbidden,
            ApiErrorCodes.Forbidden,
            "Your account does not have access to this resource."),
        new CategoryCase(
            "NotFound",
            ApiProblemDetailsFactory.NotFound,
            StatusCodes.Status404NotFound,
            ApiErrorCodes.NotFound,
            "The requested resource could not be found."),
        new CategoryCase(
            "ConcurrencyConflict",
            ApiProblemDetailsFactory.ConcurrencyConflict,
            StatusCodes.Status409Conflict,
            ApiErrorCodes.ConcurrencyConflict,
            "Reload the resource and try again."),
        new CategoryCase(
            "InternalError",
            ApiProblemDetailsFactory.InternalError,
            StatusCodes.Status500InternalServerError,
            ApiErrorCodes.InternalError,
            "An unexpected error occurred while processing the request. Reference the support ID if you contact support."),
    ];

    [Theory]
    [MemberData(nameof(Categories))]
    public void EachCategory_SetsConventionalStatusSafeErrorCodeAndDefaultDetail(CategoryCase category)
    {
        var problem = category.Factory(KnownContext, null);

        Assert.Equal(category.ExpectedStatus, problem.Status);
        Assert.Equal(category.ExpectedErrorCode, problem.Extensions[ApiProblemDetailsExtensions.ErrorCode]);
        Assert.Equal(category.ExpectedDefaultDetail, problem.Detail);
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
    }

    [Fact]
    public void EveryCategory_UsesADistinctStatusAndErrorCode_SoFailuresRemainDistinguishable()
    {
        var problems = new ProblemDetails[]
        {
            ApiProblemDetailsFactory.Validation(KnownContext),
            ApiProblemDetailsFactory.AuthenticationRequired(KnownContext),
            ApiProblemDetailsFactory.Forbidden(KnownContext),
            ApiProblemDetailsFactory.NotFound(KnownContext),
            ApiProblemDetailsFactory.ConcurrencyConflict(KnownContext),
            ApiProblemDetailsFactory.InternalError(KnownContext),
        };

        Assert.Equal(problems.Length, problems.Select(p => p.Status).Distinct().Count());
        Assert.Equal(problems.Length, problems.Select(p => p.Extensions[ApiProblemDetailsExtensions.ErrorCode]).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Categories))]
    public void EachCategory_PopulatesTraceIdAndSupportReferenceIdFromRequestContext(CategoryCase category)
    {
        var problem = category.Factory(KnownContext, null);

        Assert.Equal(KnownContext.TraceId, problem.Extensions[ApiProblemDetailsExtensions.TraceId]);
        Assert.Equal(KnownContext.CorrelationId, problem.Extensions[ApiProblemDetailsExtensions.SupportReferenceId]);
    }

    [Fact]
    public void Validation_WithFieldErrors_PopulatesPropertyToMessagesShape()
    {
        var problem = ApiProblemDetailsFactory.Validation(
            KnownContext,
            fieldErrors: new Dictionary<string, string[]>
            {
                ["email"] = ["Email is required.", "Email must be a valid address."],
                ["phone"] = ["Phone is required."],
            });

        Assert.Equal(["Email is required.", "Email must be a valid address."], problem.Errors["email"]);
        Assert.Equal(["Phone is required."], problem.Errors["phone"]);
    }

    [Fact]
    public void Validation_WithoutFieldErrors_HasEmptyErrorsRatherThanNull()
    {
        var problem = ApiProblemDetailsFactory.Validation(KnownContext);

        Assert.NotNull(problem.Errors);
        Assert.Empty(problem.Errors);
    }

    [Theory]
    [MemberData(nameof(Categories))]
    public void SafeShortDetail_IsUsedVerbatim(CategoryCase category)
    {
        const string safeDetail = "The requested Client could not be located for this tenant.";

        var problem = category.Factory(KnownContext, safeDetail);

        Assert.Equal(safeDetail, problem.Detail);
    }

    [Theory]
    [MemberData(nameof(Categories))]
    public void StackTraceShapedDetail_IsDiscardedInFavorOfTheSafeDefault(CategoryCase category)
    {
        const string stackTrace =
            "System.InvalidOperationException: Cannot open connection to 'sql-server-1'\n" +
            "   at ProjectChicago.Crm.Core.Data.ClientRepository.GetAsync() in /src/ClientRepository.cs:line 42\n" +
            "   at ProjectChicago.Crm.Facades.ClientFacade.GetAsync()";

        var problem = category.Factory(KnownContext, stackTrace);

        Assert.Equal(category.ExpectedDefaultDetail, problem.Detail);
        Assert.DoesNotContain('\n', problem.Detail!);
    }

    [Theory]
    [MemberData(nameof(Categories))]
    public void OversizedDetail_IsDiscardedInFavorOfTheSafeDefault(CategoryCase category)
    {
        var oversized = "Violation of PRIMARY KEY constraint 'PK_Clients'. " + new string('x', 400);

        var problem = category.Factory(KnownContext, oversized);

        Assert.Equal(category.ExpectedDefaultDetail, problem.Detail);
    }

    [Fact]
    public void SerializedInternalError_ContainsNoStackTraceSqlOrBrokerDetail()
    {
        const string unsafeDetail =
            "Microsoft.Data.SqlClient.SqlException (0x80131904): A network-related or instance-specific error " +
            "occurred while establishing a connection to SQL Server. Azure.Messaging.ServiceBus.ServiceBusException: " +
            "The messaging entity 'ProjectChicago.Events' could not be found.\n   at System.Data.SqlClient.SqlConnection.Open()";

        var problem = ApiProblemDetailsFactory.InternalError(KnownContext, unsafeDetail);

        var json = JsonSerializer.Serialize(problem);

        Assert.DoesNotContain("SqlException", json);
        Assert.DoesNotContain("SqlClient", json);
        Assert.DoesNotContain("ServiceBusException", json);
        Assert.DoesNotContain("ProjectChicago.Events", json);
        Assert.DoesNotContain("StackTrace", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0x80131904", json);
    }

    [Fact]
    public void SerializedValidationProblem_UsingDeclaredBaseType_StillIncludesFieldErrors()
    {
        // ApiProblemDetailsFactory.Validation returns the concrete ValidationProblemDetails type
        // specifically so this holds even when a caller only serializes via JsonSerializer
        // directly (no ASP.NET Core MVC formatter to fall back on runtime-type serialization).
        ValidationProblemDetails problem = ApiProblemDetailsFactory.Validation(
            KnownContext,
            fieldErrors: new Dictionary<string, string[]> { ["email"] = ["Email is required."] });

        var json = JsonSerializer.Serialize(problem);
        using var document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.TryGetProperty("errors", out var errorsElement));
        Assert.True(errorsElement.TryGetProperty("email", out var emailErrors));
        Assert.Equal("Email is required.", emailErrors[0].GetString());
    }

    [Fact]
    public void SerializedProblem_ExposesErrorCodeTraceIdAndSupportReferenceIdAsFlatExtensionMembers()
    {
        var problem = ApiProblemDetailsFactory.NotFound(KnownContext);

        var json = JsonSerializer.Serialize(problem);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(ApiErrorCodes.NotFound, document.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal(KnownContext.TraceId, document.RootElement.GetProperty("traceId").GetString());
        Assert.Equal(KnownContext.CorrelationId, document.RootElement.GetProperty("supportReferenceId").GetString());
    }
}
