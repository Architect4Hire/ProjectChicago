namespace ProjectChicago.Identity.Functions.Outbox;

// Configuration-bound settings for the Identity outbox relay timer Function (messaging.md: "Schedule is
// configuration, not a magic string copied across Function classes"; "Batch size, lease timeout and
// retry policy are operational settings"). Bound from the "Identity:OutboxRelay" configuration section.
// The timer schedule itself is read directly from the same section by the [TimerTrigger] attribute
// via "%Identity:OutboxRelay:Schedule%", since attribute values must be app-setting references rather
// than IOptions.
public sealed class OutboxRelaySettings
{
    public string EntityName { get; set; } = string.Empty;

    public int BatchSize { get; set; } = 25;

    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(1);
}
