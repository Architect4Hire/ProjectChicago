namespace ProjectChicago.Contracts.Audit;

// The redaction boundary for EntityMutationAudited.ChangedFields/PreviousValues/NewValues
// (AUDIT-008): passwords, authentication secrets, tokens, and cryptographic material must never
// be captured. Publishers call IsForbidden before adding a field to the payload; this type does
// not itself enforce/throw, keeping it a pure guard rather than a validation pipeline.
public static class AuditSensitiveFieldNames
{
    private static readonly string[] ForbiddenSubstrings =
    [
        "password",
        "pwd",
        "secret",
        "token",
        "apikey",
        "privatekey",
        "connectionstring",
        "ssn",
        "creditcard",
        "cvv",
    ];

    public static bool IsForbidden(string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        var normalized = Normalize(fieldName);
        return ForbiddenSubstrings.Any(normalized.Contains);
    }

    private static string Normalize(string fieldName)
    {
        Span<char> buffer = stackalloc char[fieldName.Length];
        var length = 0;

        foreach (var character in fieldName)
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer[length++] = char.ToLowerInvariant(character);
            }
        }

        return new string(buffer[..length]);
    }
}
