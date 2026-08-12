namespace ProjectChicago.Shared.Messaging;

// The envelope parsed correctly but ContractVersion is not one the caller declared support for. Per
// messaging.md failure semantics this is a poison-message condition - callers should not retry
// indefinitely, and should route to dead-letter/poison handling instead.
public sealed class UnsupportedContractVersionException : Exception
{
    public string ContractType { get; }

    public int ContractVersion { get; }

    public IReadOnlyCollection<int> SupportedVersions { get; }

    public UnsupportedContractVersionException(string contractType, int contractVersion, IReadOnlyCollection<int> supportedVersions)
        : base(
            $"Contract '{contractType}' version {contractVersion} is not supported. " +
            $"Supported versions: {string.Join(", ", supportedVersions.OrderBy(v => v))}.")
    {
        ContractType = contractType;
        ContractVersion = contractVersion;
        SupportedVersions = supportedVersions;
    }
}
