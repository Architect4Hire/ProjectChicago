using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ProjectChicago.Crm.Contracts.Clients;
using Xunit;

namespace ProjectChicago.Crm.Api.Tests;

// API-006/API-007: proves the real Crm Program.cs composition root actually exposes an OpenAPI
// document (AddOpenApi/MapOpenApi) that documents POST /api/clients under its stable, versionable
// ClientsApiContract.CreateOperationId - not just that the controller responds to requests.
public class OpenApiDocumentTests
{
    private const string CrmDbConnectionStringEnvironmentVariable = "ConnectionStrings__CrmDb";

    [Fact]
    public async Task OpenApiDocument_DocumentsThePostClientsOperationUnderItsStableOperationId()
    {
        Environment.SetEnvironmentVariable(
            CrmDbConnectionStringEnvironmentVariable,
            "Server=localhost;Database=CrmDbOpenApiDocumentTests;TrustServerCertificate=True;");

        using var factory = new WebApplicationFactory<Program>();
        using var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var postOperation = root
            .GetProperty("paths")
            .GetProperty($"/{ClientsApiContract.Route}")
            .GetProperty("post");

        Assert.Equal(ClientsApiContract.CreateOperationId, postOperation.GetProperty("operationId").GetString());
        Assert.True(postOperation.GetProperty("responses").TryGetProperty("201", out _));
    }
}
