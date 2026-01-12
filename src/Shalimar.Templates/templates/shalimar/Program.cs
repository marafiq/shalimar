using Shalimar;
using Shalimar.Vite;
using ShalimarApp.Features.V2.Welcome;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddShalimar<AppContextModel>(options =>
{
    options.ContextFactory = (sp, http) => new AppContextModel
    {
        Environment = builder.Environment.EnvironmentName
    };
});

builder.Services.AddShalimarVite();

var app = builder.Build();
app.UseShalimar();
app.UseShalimarVite(app.Environment);

static AppContextModel MakeContext(WebApplication app) => new() { Environment = app.Environment.EnvironmentName };

app.MapGet("/", (HttpContext http) =>
{
    var props = new WelcomeProps("Welcome to Shalimar");
    return ShalimarTypedResults.Component(MakeContext(app), props, "Shalimar");
}).ForTsxFile("Features/V2/Welcome/WelcomePage.tsx").AsComponent<WelcomeProps>();

app.Run();

public sealed record AppContextModel
{
    public required string Environment { get; init; }
}

