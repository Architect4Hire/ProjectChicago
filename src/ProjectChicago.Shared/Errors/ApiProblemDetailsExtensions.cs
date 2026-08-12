namespace ProjectChicago.Shared.Errors;

// Well-known ProblemDetails.Extensions keys populated by ApiProblemDetailsFactory (ERROR-001,
// ERROR-004, ERROR-005). Named here so producers and callers agree on the wire shape without
// re-typing magic strings.
public static class ApiProblemDetailsExtensions
{
    public const string ErrorCode = "errorCode";
    public const string TraceId = "traceId";
    public const string SupportReferenceId = "supportReferenceId";
}
