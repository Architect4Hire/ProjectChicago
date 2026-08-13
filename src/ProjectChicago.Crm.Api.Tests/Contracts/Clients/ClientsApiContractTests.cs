using ProjectChicago.Crm.Contracts.Clients;
using Xunit;

namespace ProjectChicago.Crm.Api.Tests.Contracts.Clients;

// Guards the stable public route/operation-id/policy coordinates (API-002, API-007) reserved by
// ClientsApiContract against silent drift once a controller is implemented against them.
public class ClientsApiContractTests
{
    [Fact]
    public void Route_MatchesTheDocumentedApiPathConvention()
    {
        Assert.Equal("api/clients", ClientsApiContract.Route);
    }

    [Fact]
    public void CreateOperationId_IsStable()
    {
        Assert.Equal("Clients_Create", ClientsApiContract.CreateOperationId);
    }

    [Fact]
    public void RequiredAuthorizationPolicy_IsStable()
    {
        Assert.Equal("Clients.Write", ClientsApiContract.RequiredAuthorizationPolicy);
    }

    [Fact]
    public void ListOperationId_IsStable()
    {
        Assert.Equal("Clients_List", ClientsApiContract.ListOperationId);
    }

    [Fact]
    public void RequiredReadAuthorizationPolicy_IsStable()
    {
        Assert.Equal("Clients.Read", ClientsApiContract.RequiredReadAuthorizationPolicy);
    }

    [Fact]
    public void DefaultPage_IsOne()
    {
        Assert.Equal(1, ClientsApiContract.DefaultPage);
    }

    [Fact]
    public void DefaultPageSize_IsWithinMaxPageSize()
    {
        Assert.InRange(ClientsApiContract.DefaultPageSize, 1, ClientsApiContract.MaxPageSize);
    }

    [Fact]
    public void MaxPageSize_IsPositiveAndBounded()
    {
        Assert.True(ClientsApiContract.MaxPageSize > 0);
    }
}
