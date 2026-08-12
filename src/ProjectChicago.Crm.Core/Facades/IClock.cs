namespace ProjectChicago.Crm.Core.Facades;

// Mechanism-neutral seam a Facade uses to resolve "now" (onion-boundaries.md: Facade "depends only
// on Business interfaces and abstractions such as current user, clock, cache, and correlation
// context"), so ClientFacade never calls DateTime.UtcNow directly and unit tests can supply a fixed
// instant. An HTTP host/Functions composition root wires the concrete adapter.
public interface IClock
{
    DateTime UtcNow { get; }
}
