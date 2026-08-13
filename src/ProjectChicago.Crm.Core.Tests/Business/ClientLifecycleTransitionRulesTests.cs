using ProjectChicago.Crm.Core.Business;
using ProjectChicago.Crm.Core.Models.DataModels.Entities;
using Xunit;

namespace ProjectChicago.Crm.Core.Tests.Business;

// Pure unit tests for the CLIENT-010..015 transition-graph decision, isolated from
// ClientBusinessTests' orchestration-focused tests.
public class ClientLifecycleTransitionRulesTests
{
    [Theory]
    [InlineData(ClientLifecycleStatus.Lead, ClientLifecycleStatus.Prospect)]
    [InlineData(ClientLifecycleStatus.Prospect, ClientLifecycleStatus.Active)]
    [InlineData(ClientLifecycleStatus.Active, ClientLifecycleStatus.OnHold)]
    [InlineData(ClientLifecycleStatus.OnHold, ClientLifecycleStatus.Active)]
    [InlineData(ClientLifecycleStatus.Active, ClientLifecycleStatus.Inactive)]
    [InlineData(ClientLifecycleStatus.Inactive, ClientLifecycleStatus.Active)]
    [InlineData(ClientLifecycleStatus.Lead, ClientLifecycleStatus.Archived)]
    [InlineData(ClientLifecycleStatus.Prospect, ClientLifecycleStatus.Archived)]
    [InlineData(ClientLifecycleStatus.Active, ClientLifecycleStatus.Archived)]
    [InlineData(ClientLifecycleStatus.OnHold, ClientLifecycleStatus.Archived)]
    [InlineData(ClientLifecycleStatus.Inactive, ClientLifecycleStatus.Archived)]
    public void IsAllowed_ForANonArchivedSourceToADifferentStatus_ReturnsTrue(
        ClientLifecycleStatus from, ClientLifecycleStatus to)
    {
        Assert.True(ClientLifecycleTransitionRules.IsAllowed(from, to));
    }

    [Theory]
    [InlineData(ClientLifecycleStatus.Lead)]
    [InlineData(ClientLifecycleStatus.Prospect)]
    [InlineData(ClientLifecycleStatus.Active)]
    [InlineData(ClientLifecycleStatus.OnHold)]
    [InlineData(ClientLifecycleStatus.Inactive)]
    [InlineData(ClientLifecycleStatus.Archived)]
    public void IsAllowed_ForTheSameStatusOnBothSides_ReturnsFalse(ClientLifecycleStatus status)
    {
        Assert.False(ClientLifecycleTransitionRules.IsAllowed(status, status));
    }

    [Theory]
    [InlineData(ClientLifecycleStatus.Lead)]
    [InlineData(ClientLifecycleStatus.Prospect)]
    [InlineData(ClientLifecycleStatus.Active)]
    [InlineData(ClientLifecycleStatus.OnHold)]
    [InlineData(ClientLifecycleStatus.Inactive)]
    public void IsAllowed_FromArchivedToAnythingElse_ReturnsFalse(ClientLifecycleStatus to)
    {
        Assert.False(ClientLifecycleTransitionRules.IsAllowed(ClientLifecycleStatus.Archived, to));
    }

    [Fact]
    public void IsAllowed_WithAnUndefinedFromOrToValue_ReturnsFalse()
    {
        Assert.False(ClientLifecycleTransitionRules.IsAllowed((ClientLifecycleStatus)999, ClientLifecycleStatus.Active));
        Assert.False(ClientLifecycleTransitionRules.IsAllowed(ClientLifecycleStatus.Active, (ClientLifecycleStatus)999));
    }
}
