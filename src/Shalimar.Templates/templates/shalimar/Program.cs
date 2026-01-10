using ShalimarApp;
using Shalimar;

var builder = WebApplication.CreateBuilder(args);

// Add Shalimar services with context factory
builder.Services.AddShalimar<AppContextModel>(options =>
{
    options.ContextFactory = (sp, http) => new AppContextModel
    {
        Environment = builder.Environment.EnvironmentName
    };
});

var app = builder.Build();
app.UseShalimar();

// Serve the shell HTML for the home route
app.MapGet("/", async (HttpContext http) =>
{
    var context = new AppContextModel { Environment = app.Environment.EnvironmentName };
    var props = new HomeProps("Welcome to Shalimar");
    return await http.RenderComponent(context, props, "Shalimar App");
}).AsComponent<HomeProps>();

app.Run();
