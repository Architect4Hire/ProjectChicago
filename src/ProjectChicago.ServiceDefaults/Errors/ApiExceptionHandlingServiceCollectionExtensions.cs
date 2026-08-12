using Microsoft.Extensions.DependencyInjection;

namespace ProjectChicago.ServiceDefaults.Errors;

public static class ApiExceptionHandlingServiceCollectionExtensions
{
    // Opt-in per HTTP host (Program.cs), not part of AddServiceDefaults - this is ASP.NET Core
    // HTTP pipeline wiring (ProblemDetails/IExceptionHandler) that a Functions project does not
    // have a request pipeline to hang (backend.md: "exception/ProblemDetails wiring" is a host
    // responsibility). Call together with app.UseExceptionHandler() in the composition root.
    public static IServiceCollection AddApiExceptionHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = ApiProblemDetailsCustomizer.Customize);
        return services;
    }
}
