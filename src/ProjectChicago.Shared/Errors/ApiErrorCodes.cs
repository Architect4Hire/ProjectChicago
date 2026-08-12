namespace ProjectChicago.Shared.Errors;

// Stable, documented, machine-readable error codes (ERROR-003, API-006). These are the wire
// contract - do not rename an existing value; add a new one instead.
public static class ApiErrorCodes
{
    public const string Validation = "validation_failed";
    public const string AuthenticationRequired = "authentication_required";
    public const string Forbidden = "forbidden";
    public const string NotFound = "resource_not_found";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string InternalError = "internal_error";
}
