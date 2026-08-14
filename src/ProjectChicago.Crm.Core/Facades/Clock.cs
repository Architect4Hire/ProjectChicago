namespace ProjectChicago.Crm.Core.Facades;

// Simple IClock implementation that provides the current UTC time. Wired in the HTTP host
// composition root so Facades have a testable abstraction for "now" without coupling to
// DateTime.UtcNow directly. The Facade never calls DateTime.UtcNow - it always delegates to
// this abstraction so unit tests can inject a fixed instant (onion-boundaries.md).
public sealed class Clock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
