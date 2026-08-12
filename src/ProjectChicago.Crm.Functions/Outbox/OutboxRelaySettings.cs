namespace ProjectChicago.Crm.Functions.Outbox;

// Configuration-bound settings for the CRM outbox relay timer Function (messaging.md: "Schedule is
// configuration, not a magic string copied across Function classes"; "Batch size, lease timeout and
// retry policy are operational settings"). Bound from the "Crm:OutboxRelay" configuration section.
// The timer schedule itself is read directly from the same section by the [TimerTrigger] attribute
// via "%Crm:OutboxRelay:Schedule%", since attribute values must be app-setting references rather
// than IOptions.
public sealed class OutboxRelaySettings
{
    public string EntityName { get; set; } = string.Empty;

    public int BatchSize { get; set; } = 25;

    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(1);
}
