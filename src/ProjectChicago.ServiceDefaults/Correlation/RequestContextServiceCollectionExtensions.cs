using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ProjectChicago.Shared.Correlation;

namespace ProjectChicago.ServiceDefaults.Correlation;

public static class RequestContextServiceCollectionExtensions
{
    // Opt-in per HTTP host (Program.cs) rather than folded into AddServiceDefaults, which the
    // sibling Functions project also calls and has no HttpContext to adapt (aspire.md).
    public static IServiceCollection AddHttpRequestContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentRequestContext, HttpRequestContextAccessor>();
        return services;
    }
}
