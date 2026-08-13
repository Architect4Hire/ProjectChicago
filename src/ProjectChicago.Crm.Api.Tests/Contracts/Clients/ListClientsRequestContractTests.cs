using System.ComponentModel.DataAnnotations;
using ProjectChicago.Crm.Contracts.Clients;
using Xunit;

namespace ProjectChicago.Crm.Api.Tests.Contracts.Clients;

// Locks the GET /api/clients query contract (CLIENT-020..024, API-005): default/max pagination
// and enum-filter/sort validation, independently of any future controller/query implementation
// (add-endpoint.md step 2: "Transport model validation catches shape/format").
public class ListClientsRequestContractTests
{
    [Fact]
    public void Page_WhenOmitted_DefaultsToClientsApiContractDefaultPage()
    {
        var request = new ListClientsRequest();

        Assert.Equal(ClientsApiContract.DefaultPage, request.Page);
    }

    [Fact]
    public void PageSize_WhenOmitted_DefaultsToClientsApiContractDefaultPageSize()
    {
        var request = new ListClientsRequest();

        Assert.Equal(ClientsApiContract.DefaultPageSize, request.PageSize);
    }

    [Fact]
    public void Validate_DefaultRequest_ProducesNoValidationErrors()
    {
        var errors = Validate(new ListClientsRequest());

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_PageAtMinimum_IsAccepted()
    {
        var request = new ListClientsRequest { Page = 1 };

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_PageBelowOne_IsRejected(int page)
    {
        var request = new ListClientsRequest { Page = page };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ListClientsRequest.Page)));
    }

    [Fact]
    public void Validate_PageSizeAtMax_IsAccepted()
    {
        var request = new ListClientsRequest { PageSize = ClientsApiContract.MaxPageSize };

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void Validate_PageSizeAboveMax_IsRejected()
    {
        var request = new ListClientsRequest { PageSize = ClientsApiContract.MaxPageSize + 1 };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ListClientsRequest.PageSize)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_PageSizeBelowOne_IsRejected(int pageSize)
    {
        var request = new ListClientsRequest { PageSize = pageSize };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ListClientsRequest.PageSize)));
    }

    [Theory]
    [InlineData(ClientLifecycleStatusContract.Lead)]
    [InlineData(ClientLifecycleStatusContract.Prospect)]
    [InlineData(ClientLifecycleStatusContract.Active)]
    [InlineData(ClientLifecycleStatusContract.OnHold)]
    [InlineData(ClientLifecycleStatusContract.Inactive)]
    [InlineData(ClientLifecycleStatusContract.Archived)]
    public void Validate_DefinedLifecycleStatus_IsAccepted(ClientLifecycleStatusContract status)
    {
        var request = new ListClientsRequest { LifecycleStatus = status };

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void Validate_UndefinedLifecycleStatus_IsRejected()
    {
        var request = new ListClientsRequest { LifecycleStatus = (ClientLifecycleStatusContract)999 };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ListClientsRequest.LifecycleStatus)));
    }

    [Theory]
    [InlineData(ClientSortField.Name)]
    [InlineData(ClientSortField.CreatedAtUtc)]
    [InlineData(ClientSortField.LastModifiedAtUtc)]
    [InlineData(ClientSortField.LifecycleStatus)]
    public void Validate_DefinedSortField_IsAccepted(ClientSortField sortField)
    {
        var request = new ListClientsRequest { SortBy = sortField };

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void Validate_UndefinedSortField_IsRejected()
    {
        var request = new ListClientsRequest { SortBy = (ClientSortField)999 };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ListClientsRequest.SortBy)));
    }

    [Theory]
    [InlineData(ClientSortDirection.Ascending)]
    [InlineData(ClientSortDirection.Descending)]
    public void Validate_DefinedSortDirection_IsAccepted(ClientSortDirection sortDirection)
    {
        var request = new ListClientsRequest { SortDirection = sortDirection };

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void Validate_UndefinedSortDirection_IsRejected()
    {
        var request = new ListClientsRequest { SortDirection = (ClientSortDirection)999 };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ListClientsRequest.SortDirection)));
    }

    [Fact]
    public void Validate_SearchLongerThanTwoHundredCharacters_IsRejected()
    {
        var request = new ListClientsRequest { Search = new string('a', 201) };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ListClientsRequest.Search)));
    }

    [Fact]
    public void Validate_OwnerUserIdLongerThanOneHundredTwentyEightCharacters_IsRejected()
    {
        var request = new ListClientsRequest { OwnerUserId = new string('a', 129) };

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ListClientsRequest.OwnerUserId)));
    }

    private static IReadOnlyList<ValidationResult> Validate(ListClientsRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
