namespace ProjectChicago.Shared.Correlation;

// Mechanism-neutral seam (backend.md: Facade "depends only on ... abstractions such as current
// user, clock, cache, and correlation context") so Facades/Business resolve the active
// RequestContext/ActorContext through DI instead of depending on HttpContext or a Service Bus
// message envelope directly. Each entry-point kind supplies its own adapter: HTTP hosts populate
// this from HttpContext (ServiceDefaults.Correlation.HttpRequestContextAccessor); a Functions
// project would populate it from the consumed message envelope instead.
public interface ICurrentRequestContext
{
    RequestContext Current { get; }
}
