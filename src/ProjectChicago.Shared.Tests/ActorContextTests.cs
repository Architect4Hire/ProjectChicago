using System.Collections.Immutable;
using ProjectChicago.Shared.Correlation;
using Xunit;

namespace ProjectChicago.Shared.Tests;

public class ActorContextTests
{
    [Fact]
    public void Unknown_HasUnknownTypeAndNoActorId()
    {
        var actor = ActorContext.Unknown;

        Assert.Equal(ActorType.Unknown, actor.ActorType);
        Assert.Null(actor.ActorId);
        Assert.Empty(actor.Roles);
    }

    [Fact]
    public void ForSystem_HasSystemTypeAndNoActorId()
    {
        var actor = ActorContext.ForSystem();

        Assert.Equal(ActorType.System, actor.ActorType);
        Assert.Null(actor.ActorId);
    }

    [Fact]
    public void ForAnonymous_HasAnonymousTypeAndNoActorId()
    {
        var actor = ActorContext.ForAnonymous();

        Assert.Equal(ActorType.Anonymous, actor.ActorType);
        Assert.Null(actor.ActorId);
    }

    [Fact]
    public void ForUser_SetsActorIdAndUserType()
    {
        var actor = ActorContext.ForUser("user-123");

        Assert.Equal(ActorType.User, actor.ActorType);
        Assert.Equal("user-123", actor.ActorId);
        Assert.Empty(actor.Roles);
    }

    [Fact]
    public void ForUser_SetsRolesWhenSupplied()
    {
        var roles = ImmutableArray.Create("Sales", "Manager");

        var actor = ActorContext.ForUser("user-123", roles);

        Assert.Equal(roles, actor.Roles);
    }

    [Fact]
    public void ForService_SetsActorIdAndServiceType()
    {
        var actor = ActorContext.ForService("crm-outbox-relay");

        Assert.Equal(ActorType.Service, actor.ActorType);
        Assert.Equal("crm-outbox-relay", actor.ActorId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ForUser_RejectsMissingActorId(string? actorId)
    {
        Assert.Throws<ArgumentException>(() => ActorContext.ForUser(actorId!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ForService_RejectsMissingActorId(string? actorId)
    {
        Assert.Throws<ArgumentException>(() => ActorContext.ForService(actorId!));
    }
}
