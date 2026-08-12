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
}
