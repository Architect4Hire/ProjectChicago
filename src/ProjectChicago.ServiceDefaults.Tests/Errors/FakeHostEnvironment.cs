using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ProjectChicago.ServiceDefaults.Tests.Errors;

public sealed class FakeHostEnvironment : IHostEnvironment
{
    public string ApplicationName { get; set; } = "ProjectChicago.Crm";

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

    public string ContentRootPath { get; set; } = string.Empty;

    public string EnvironmentName { get; set; } = Environments.Development;
}
