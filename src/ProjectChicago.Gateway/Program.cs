using ProjectChicago.Gateway.Correlation;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.UseCorrelation();

app.MapDefaultEndpoints();

app.MapGet("/", () => "Hello World!");

app.Run();
