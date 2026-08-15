using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ProjectChicago.ServiceDefaults.Filters;

/// <summary>
/// Action filter that ensures the request is authenticated (SEC-004, SEC-010).
/// Returns 401 Unauthorized if User.Identity is not authenticated.
/// Allows fine-grained (403 Forbidden) policy authorization to continue in [Authorize(Policy = "...")] attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireAuthenticationAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.User.Identity is not { IsAuthenticated: true })
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }
}
