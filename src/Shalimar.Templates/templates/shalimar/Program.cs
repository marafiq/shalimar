using ShalimarApp;
using ShalimarApp.Features.Accounts;
using ShalimarApp.Features.Home;
using ShalimarApp.Features.Settings;
using ShalimarApp.Features.Tasks;
using ShalimarApp.Features.Tasks.Board;
using Shalimar;
using Shalimar.Vite;

var builder = WebApplication.CreateBuilder(args);

// Add Shalimar services with context factory
builder.Services.AddShalimar<AppContextModel>(options =>
{
    options.ContextFactory = (sp, http) => new AppContextModel
    {
        Environment = builder.Environment.EnvironmentName
    };
});

// Enable Vite proxying + HMR in Development
builder.Services.AddShalimarVite();

var app = builder.Build();
app.UseShalimar();
app.UseShalimarVite(app.Environment);

static AppContextModel MakeContext(WebApplication app) => new() { Environment = app.Environment.EnvironmentName };

// NOTE: Shalimar is server-first. We map every route that should render a client component.
// These endpoints are the source of truth for route generation (via .AsComponent<T>()).

app.MapGet("/", async (HttpContext http) =>
{
    var props = new DashboardProps("Welcome to Shalimar");
    return await http.RenderComponent(MakeContext(app), props, "Shalimar App");
}).AsComponent<DashboardProps>();

app.MapGet("/tasks", async (HttpContext http) =>
{
    var props = new TasksProps("Tasks");
    return await http.RenderComponent(MakeContext(app), props, "Shalimar App");
}).AsComponent<TasksProps>();

app.MapGet("/tasks/board", async (HttpContext http) =>
{
    var props = new TaskBoardProps("Board");
    return await http.RenderComponent(MakeContext(app), props, "Shalimar App");
}).AsComponent<TaskBoardProps>();

app.MapGet("/tasks/{taskId}", async (HttpContext http, string taskId) =>
{
    var props = new TaskDetailProps("Task", taskId);
    return await http.RenderComponent(MakeContext(app), props, "Shalimar App");
}).AsComponent<TaskDetailProps>();

app.MapGet("/accounts", async (HttpContext http) =>
{
    var props = new AccountsProps("Accounts");
    return await http.RenderComponent(MakeContext(app), props, "Shalimar App");
}).AsComponent<AccountsProps>();

app.MapGet("/accounts/{accountId}", async (HttpContext http, string accountId) =>
{
    var props = new AccountDetailProps("Account", accountId);
    return await http.RenderComponent(MakeContext(app), props, "Shalimar App");
}).AsComponent<AccountDetailProps>();

app.MapGet("/settings", async (HttpContext http) =>
{
    var props = new SettingsProps("Settings");
    return await http.RenderComponent(MakeContext(app), props, "Shalimar App");
}).AsComponent<SettingsProps>();

app.Run();
