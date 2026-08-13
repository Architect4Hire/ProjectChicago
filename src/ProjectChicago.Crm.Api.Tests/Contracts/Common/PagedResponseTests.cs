using System.Text.Json;
using ProjectChicago.Crm.Contracts.Clients;
using ProjectChicago.Crm.Contracts.Common;
using Xunit;

namespace ProjectChicago.Crm.Api.Tests.Contracts.Common;

// Locks the shared collection-response envelope's wire shape (api-contracts.md's "shared
// pagination envelope") using ClientResponse as the item type, since GET api/clients (CLIENT-020..
// 024, API-005) is the envelope's first consumer.
public class PagedResponseTests
{
    [Fact]
    public void Serialize_Response_UsesCamelCasePropertyNamesForEnvelopeFields()
    {
        var envelope = new PagedResponse<ClientResponse>
        {
            Items =
            [
                new ClientResponse
                {
                    Id = Guid.NewGuid(),
                    Name = "Acme Corporation",
                    LifecycleStatus = ClientLifecycleStatusContract.Active,
                    OwnerUserId = "user-42",
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedBy = "user-42",
                    LastModifiedAtUtc = DateTime.UtcNow,
                    LastModifiedBy = "user-42",
                    ConcurrencyToken = "AAAAAAAAB9E=",
                },
            ],
            Page = 2,
            PageSize = 25,
            TotalCount = 30,
            TotalPages = 2,
        };

        var json = JsonSerializer.Serialize(envelope);
        var root = JsonDocument.Parse(json).RootElement;

        Assert.Equal(JsonValueKind.Array, root.GetProperty("items").ValueKind);
        Assert.Equal(1, root.GetProperty("items").GetArrayLength());
        Assert.Equal(2, root.GetProperty("page").GetInt32());
        Assert.Equal(25, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(30, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, root.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public void Serialize_EmptyItems_ProducesAnEmptyArrayNotNull()
    {
        var envelope = new PagedResponse<ClientResponse>
        {
            Items = [],
            Page = 1,
            PageSize = 25,
            TotalCount = 0,
            TotalPages = 0,
        };

        var json = JsonSerializer.Serialize(envelope);
        var root = JsonDocument.Parse(json).RootElement;

        Assert.Equal(JsonValueKind.Array, root.GetProperty("items").ValueKind);
        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public void Deserialize_RoundTrips_PreservingPaginationMetadata()
    {
        var envelope = new PagedResponse<ClientResponse>
        {
            Items = [],
            Page = 3,
            PageSize = 10,
            TotalCount = 21,
            TotalPages = 3,
        };

        var json = JsonSerializer.Serialize(envelope);
        var roundTripped = JsonSerializer.Deserialize<PagedResponse<ClientResponse>>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(envelope.Page, roundTripped!.Page);
        Assert.Equal(envelope.PageSize, roundTripped.PageSize);
        Assert.Equal(envelope.TotalCount, roundTripped.TotalCount);
        Assert.Equal(envelope.TotalPages, roundTripped.TotalPages);
    }
}
