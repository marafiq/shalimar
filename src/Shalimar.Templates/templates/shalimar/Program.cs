using ShalimarApp;
using ShalimarApp.Features.Accounts;
using ShalimarApp.Features.Crm;
using ShalimarApp.Features.Home;
using ShalimarApp.Features.Settings;
using ShalimarApp.Features.Tasks;
using ShalimarApp.Features.Tasks.Board;
using FluentValidation;
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

// Server-owned CRM state (in-memory for now; later backed by DB)
builder.Services.AddSingleton<CrmRepository>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateTaskRequestValidator>();

var app = builder.Build();
app.UseShalimar();
app.UseShalimarVite(app.Environment);

static AppContextModel MakeContext(WebApplication app) => new() { Environment = app.Environment.EnvironmentName };
static IDictionary<string, string[]> ToValidationProblem(FluentValidation.Results.ValidationResult result) =>
    result.Errors
        .GroupBy(e => e.PropertyName)
        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

// NOTE: Shalimar is server-first. We map every route that should render a client component.
// These endpoints are the source of truth for route generation (via .AsComponent<T>()).

app.MapGet("/", async (HttpContext http, CrmRepository repo) =>
{
    var snapshot = repo.Snapshot();
    var open = snapshot.Tasks.Count(t => t.Status != "done");
    var overdue = snapshot.Tasks.Count(t => t.DueAt is not null && t.DueAt.Value < DateTimeOffset.UtcNow && t.Status != "done");
    var props = new DashboardProps(
        Message: "Welcome to Shalimar",
        OpenTasks: open,
        OverdueTasks: overdue,
        Accounts: snapshot.Accounts.Count,
        FocusTasks: snapshot.Tasks.Take(5).ToList(),
        Activity: snapshot.Activity.Take(12).ToList(),
        Insights: Shalimar.Generated.DeferredRefs.CrmInsights());
    return await http.RenderComponent(MakeContext(app), props, "Shalimar App");
}).AsComponent<DashboardProps>();

// Deferred mode: resolves after hydration via typed handle in props.
app.MapGet("/crm/insights", (CrmRepository repo) => repo.Insights())
    .AsDeferred<CrmInsightsDto>();

app.MapGet("/tasks", async (HttpContext http, CrmRepository repo) =>
{
    var props = new TasksProps("Tasks", repo.Snapshot());
    return await http.RenderComponent(MakeContext(app), props, "Shalimar App");
}).AsComponent<TasksProps>();

app.MapGet("/tasks/board", async (HttpContext http, CrmRepository repo) =>
{
    var props = new TaskBoardProps("Board", repo.Snapshot());
    return await http.RenderComponent(MakeContext(app), props, "Shalimar App");
}).AsComponent<TaskBoardProps>();

app.MapGet("/tasks/{taskId}", async (HttpContext http, string taskId) =>
{
    var props = new TaskDetailProps("Task", taskId);
    return await http.RenderComponent(MakeContext(app), props, "Shalimar App");
}).AsComponent<TaskDetailProps>();

app.MapGet("/accounts", async (HttpContext http, CrmRepository repo) =>
{
    var props = new AccountsProps("Accounts", repo.Snapshot());
    return await http.RenderComponent(MakeContext(app), props, "Shalimar App");
}).AsComponent<AccountsProps>();

app.MapGet("/accounts/{accountId}", async (HttpContext http, string accountId, CrmRepository repo) =>
{
    var account = repo.Accounts.FirstOrDefault(a => a.Id == accountId);
    var contacts = repo.ContactsByAccount(accountId);
    var owner = account is null ? null : repo.Users.FirstOrDefault(u => u.Id == account.OwnerId);
    var props = new AccountDetailProps("Account", accountId, account, contacts, owner);
    return await http.RenderComponent(MakeContext(app), props, "Shalimar App");
}).AsComponent<AccountDetailProps>();

app.MapGet("/settings", async (HttpContext http) =>
{
    var props = new SettingsProps("Settings", app.Environment.EnvironmentName);
    return await http.RenderComponent(MakeContext(app), props, "Shalimar App");
}).AsComponent<SettingsProps>();

// Shalimar-style "feature endpoints" (no /api). These will be replaced by generated hooks later.
app.MapGet("/crm/snapshot", (CrmRepository repo) => Results.Ok(repo.Snapshot()));
app.MapGet("/crm/tasks/{taskId}/messages", (CrmRepository repo, string taskId) => Results.Ok(repo.Messages(taskId)));
app.MapGet("/crm/tasks/{taskId}/artifacts", (CrmRepository repo, string taskId) => Results.Ok(repo.Artifacts(taskId)));
app.MapGet("/crm/tasks/{taskId}/decisions", (CrmRepository repo, string taskId) => Results.Ok(repo.Decisions(taskId)));
app.MapGet("/crm/accounts/{accountId}/contacts", (CrmRepository repo, string accountId) => Results.Ok(repo.ContactsByAccount(accountId)));

app.MapPost("/crm/tasks", async (CrmRepository repo, IValidator<CreateTaskRequest> v, CreateTaskRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    return Results.Ok(repo.CreateTask(req));
});

app.MapPatch("/crm/tasks/{taskId}", async (CrmRepository repo, IValidator<UpdateTaskRequest> v, string taskId, UpdateTaskRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    var updated = repo.UpdateTask(taskId, req);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
});

app.MapPost("/crm/tasks/{taskId}/move", async (CrmRepository repo, IValidator<MoveTaskRequest> v, string taskId, MoveTaskRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    var updated = repo.MoveTask(taskId, req);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
});

app.MapPost("/crm/tasks/{taskId}/messages", async (CrmRepository repo, IValidator<PostMessageRequest> v, string taskId, PostMessageRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    return Results.Ok(repo.PostMessage(taskId, req));
});

app.MapPost("/crm/tasks/{taskId}/artifacts", async (CrmRepository repo, IValidator<AddArtifactRequest> v, string taskId, AddArtifactRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    return Results.Ok(repo.AddArtifact(taskId, req));
});

app.MapPost("/crm/tasks/{taskId}/decisions", async (CrmRepository repo, IValidator<AddDecisionRequest> v, string taskId, AddDecisionRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    return Results.Ok(repo.AddDecision(taskId, req));
});

app.Run();
