using System.Collections.Immutable;

namespace ProjectChicago.Shared.Correlation;

public readonly record struct ActorContext
{
    public string? ActorId { get; }

    public ActorType ActorType { get; }

    public ImmutableArray<string> Roles { get; }

    private ActorContext(string? actorId, ActorType actorType, ImmutableArray<string> roles)
    {
        ActorId = actorId;
        ActorType = actorType;
        Roles = roles.IsDefault ? ImmutableArray<string>.Empty : roles;
    }

    public static ActorContext Unknown { get; } = new(actorId: null, ActorType.Unknown, ImmutableArray<string>.Empty);

    public static ActorContext ForSystem() => new(actorId: null, ActorType.System, ImmutableArray<string>.Empty);

    public static ActorContext ForAnonymous() => new(actorId: null, ActorType.Anonymous, ImmutableArray<string>.Empty);

    public static ActorContext ForUser(string actorId, ImmutableArray<string> roles = default) =>
        new(RequireActorId(actorId), ActorType.User, roles);

    public static ActorContext ForService(string actorId, ImmutableArray<string> roles = default) =>
        new(RequireActorId(actorId), ActorType.Service, roles);

    private static string RequireActorId(string actorId) =>
        string.IsNullOrWhiteSpace(actorId)
            ? throw new ArgumentException("Actor identifier cannot be null or whitespace.", nameof(actorId))
            : actorId;
}
