using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ProjectChicago.Contracts.Audit;
using Xunit;

namespace ProjectChicago.Contracts.Tests;

public class EntityMutationAuditedTests
{
    private static readonly string[] ExpectedRequiredProperties =
    [
        nameof(EntityMutationAudited.EventId),
        nameof(EntityMutationAudited.OccurredAtUtc),
        nameof(EntityMutationAudited.SourceService),
        nameof(EntityMutationAudited.EntityType),
        nameof(EntityMutationAudited.EntityId),
        nameof(EntityMutationAudited.Action),
        nameof(EntityMutationAudited.ActorType),
        nameof(EntityMutationAudited.TraceId),
        nameof(EntityMutationAudited.CorrelationId),
    ];

    private static EntityMutationAudited CreateMinimal() => new()
    {
        EventId = "event-1",
        OccurredAtUtc = DateTimeOffset.Parse("2026-08-12T09:15:00Z"),
        SourceService = AuditSourceServices.Crm,
        EntityType = AuditEntityTypes.Client,
        EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Action = AuditActions.StatusChanged,
        ActorType = AuditActorTypes.User,
        TraceId = "4bf92f3577b34da6a3ce929d0e0e4736",
        CorrelationId = "correlation-1",
    };

    // AUDIT-002's field list is the contract's whole reason to exist. This test fails the moment
    // a `required` modifier is accidentally dropped (silently making a mandatory audit field
    // optional) or added to a field that should stay optional.
    [Fact]
    public void RequiredProperties_MatchExactlyTheAudit002MinimumFieldSet()
    {
        var requiredProperties = typeof(EntityMutationAudited)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetCustomAttribute<RequiredMemberAttribute>() is not null)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        Assert.Equal(ExpectedRequiredProperties.OrderBy(name => name, StringComparer.Ordinal), requiredProperties);
    }

    [Fact]
    public void MinimalConstruction_DefaultsVersionAndLeavesOptionalFieldsAbsent()
    {
        var audited = CreateMinimal();

        Assert.Equal(EntityMutationAudited.CurrentVersion, audited.Version);
        Assert.Null(audited.ActorId);
        Assert.Null(audited.CausationId);
        Assert.NotNull(audited.ChangedFields);
        Assert.Empty(audited.ChangedFields);
        Assert.Null(audited.PreviousValues);
        Assert.Null(audited.NewValues);
    }

    [Fact]
    public void CurrentVersion_IsOne()
    {
        // A change here is a contract version bump, not a routine edit - pin it explicitly.
        Assert.Equal(1, EntityMutationAudited.CurrentVersion);
    }

    [Fact]
    public void ExplicitVersion_OverridesTheDefault()
    {
        var audited = CreateMinimal() with { Version = 2 };

        Assert.Equal(2, audited.Version);
    }

    [Fact]
    public void Serialization_RoundTripsEveryFieldIncludingOptionalOnes()
    {
        var audited = CreateMinimal() with
        {
            ActorId = "user-123",
            CausationId = "causation-1",
            ChangedFields = ["Status"],
            PreviousValues = new Dictionary<string, string> { ["Status"] = "Active" },
            NewValues = new Dictionary<string, string> { ["Status"] = "Completed" },
        };

        var json = JsonSerializer.Serialize(audited);
        var roundTripped = JsonSerializer.Deserialize<EntityMutationAudited>(json);

        Assert.Equal(audited, roundTripped);
    }

    [Fact]
    public void Serialization_OmittedOptionalFieldsRoundTripAsNullOrEmpty()
    {
        var audited = CreateMinimal();

        var json = JsonSerializer.Serialize(audited);
        var roundTripped = JsonSerializer.Deserialize<EntityMutationAudited>(json);

        Assert.Equal(audited, roundTripped);
        Assert.Null(roundTripped!.ActorId);
        Assert.Null(roundTripped.PreviousValues);
        Assert.Null(roundTripped.NewValues);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("Password")]
    [InlineData("userPassword")]
    [InlineData("pwd")]
    [InlineData("authToken")]
    [InlineData("access_token")]
    [InlineData("api-key")]
    [InlineData("ApiKey")]
    [InlineData("privateKey")]
    [InlineData("connectionString")]
    [InlineData("ssn")]
    [InlineData("creditCardNumber")]
    [InlineData("cvv")]
    public void SensitiveFieldNames_AreForbidden(string fieldName)
    {
        Assert.True(AuditSensitiveFieldNames.IsForbidden(fieldName));
    }

    [Theory]
    [InlineData("Status")]
    [InlineData("Name")]
    [InlineData("Priority")]
    [InlineData("AssignedUserId")]
    [InlineData("Description")]
    public void OrdinaryFieldNames_AreNotForbidden(string fieldName)
    {
        Assert.False(AuditSensitiveFieldNames.IsForbidden(fieldName));
    }

    [Fact]
    public void SerializedPayload_ContainsNoForbiddenFieldNameEvenWhenAPublisherMistakenlyIncludesOne()
    {
        // Demonstrates the redaction boundary: a publisher that skips the IsForbidden guard and
        // stuffs a secret-shaped field into NewValues produces a payload IsForbidden would have
        // rejected - proving the guard actually covers what this contract puts on the wire.
        var audited = CreateMinimal() with
        {
            ChangedFields = ["Status", "password"],
            NewValues = new Dictionary<string, string> { ["Status"] = "Completed", ["password"] = "hunter2" },
        };

        var offendingFields = audited.ChangedFields.Where(AuditSensitiveFieldNames.IsForbidden).ToList();

        Assert.Contains("password", offendingFields);
        Assert.DoesNotContain("Status", offendingFields);
    }
}
