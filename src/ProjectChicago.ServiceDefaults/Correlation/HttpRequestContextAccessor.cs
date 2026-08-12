using Microsoft.AspNetCore.Http;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.ServiceDefaults.Correlation;

// Scoped ICurrentRequestContext adapter for ASP.NET Core HTTP hosts (TRACE-001..007). Resolves the
// RequestContext once per request from the ambient HttpContext via HttpRequestContextFactory so
// Facades/Business obtain actor/correlation context through DI (backend.md) instead of depending
// on HttpContext directly. Registered per host via AddHttpRequestContext (Program.cs), not as part
// of AddServiceDefaults - a Functions project has no HttpContext to adapt.
public sealed class HttpRequestContextAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentRequestContext
{
    private RequestContext? _resolved;

    public RequestContext Current
    {
        get
        {
            if (_resolved is { } cached)
            {
                return cached;
            }

            var httpContext = httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException(
                    "No active HttpContext is available to resolve the current RequestContext.");

            var resolved = HttpRequestContextFactory.Create(httpContext);
            _resolved = resolved;
            return resolved;
        }
    }
}
