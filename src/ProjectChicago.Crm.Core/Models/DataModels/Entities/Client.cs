namespace ProjectChicago.Crm.Core.Models.DataModels.Entities;

// CRM's Client aggregate root (CLIENT-001..015, DATA-006..008). Id is an application-assigned GUID
// rather than a database-generated sequential value, so it is safe to expose externally without a
// separate public-identifier field (DATA-007). Construction is the only way to reach a valid Client,
// so every invariant below holds for the lifetime of the instance; state-transition rules beyond
// construction belong to the CRM Business layer (backend.md), not this entity.
public sealed class Client
{
    private Client()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? PrimaryContactName { get; private set; }

    public string? PrimaryEmail { get; private set; }

    public string? PrimaryPhone { get; private set; }

    public string? Website { get; private set; }

    public string? AddressLine { get; private set; }

    public string? City { get; private set; }

    public string? StateOrProvince { get; private set; }

    public string? PostalCode { get; private set; }

    public string? Country { get; private set; }

    public ClientLifecycleStatus LifecycleStatus { get; private set; }

    public string? Description { get; private set; }

    // The assigned owner's actor identifier, in the same string form as ActorContext.ActorId
    // (Shared/Correlation) rather than a type borrowed from an as-yet-unowned Identity store.
    public string OwnerUserId { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTime LastModifiedAtUtc { get; private set; }

    public string LastModifiedBy { get; private set; } = string.Empty;

    // Optimistic concurrency token (DATA-008). Empty until the Data layer's EF mapping assigns it;
    // that mapping is out of scope for this microstep.
    public byte[] RowVersion { get; private set; } = [];

    public static Client Create(
        Guid id,
        string name,
        ClientLifecycleStatus lifecycleStatus,
        string ownerUserId,
        string createdBy,
        DateTime createdAtUtc,
        string? primaryContactName = null,
        string? primaryEmail = null,
        string? primaryPhone = null,
        string? website = null,
        string? addressLine = null,
        string? city = null,
        string? stateOrProvince = null,
        string? postalCode = null,
        string? country = null,
        string? description = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Client Id cannot be empty.", nameof(id));
        }

        if (!Enum.IsDefined(lifecycleStatus))
        {
            throw new ArgumentException("Lifecycle status must be a defined ClientLifecycleStatus value.", nameof(lifecycleStatus));
        }

        var validCreatedAtUtc = RequireUtc(createdAtUtc, nameof(createdAtUtc));

        // Last-modified metadata starts identical to created metadata; it only diverges once a later
        // Business-layer mutation touches the record.
        return new Client
        {
            Id = id,
            Name = RequireText(name, nameof(name)),
            LifecycleStatus = lifecycleStatus,
            OwnerUserId = RequireText(ownerUserId, nameof(ownerUserId)),
            CreatedBy = RequireText(createdBy, nameof(createdBy)),
            CreatedAtUtc = validCreatedAtUtc,
            LastModifiedBy = createdBy,
            LastModifiedAtUtc = validCreatedAtUtc,
            PrimaryContactName = primaryContactName,
            PrimaryEmail = primaryEmail,
            PrimaryPhone = primaryPhone,
            Website = website,
            AddressLine = addressLine,
            City = city,
            StateOrProvince = stateOrProvince,
            PostalCode = postalCode,
            Country = country,
            Description = description,
        };
    }

    private static string RequireText(string? value, string paramName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value cannot be null or whitespace.", paramName)
            : value;

    private static DateTime RequireUtc(DateTime value, string paramName) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : throw new ArgumentException("Value must be a UTC DateTime (DATA-006).", paramName);
}
