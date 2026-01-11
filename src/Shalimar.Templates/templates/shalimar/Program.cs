using ShalimarApp;
using ShalimarApp.Features.Accounts;
using ShalimarApp.Features.Crm;
using ShalimarApp.Features.Home;
using ShalimarApp.Features.Settings;
using ShalimarApp.Features.Tasks;
using ShalimarApp.Features.Tasks.Board;
using ShalimarApp.Features.V2.Workbench;
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

// Deterministic clock for repeatable UI snapshots (tests set SHALIMAR_FIXED_CLOCK=1).
var fixedClock = Environment.GetEnvironmentVariable("SHALIMAR_FIXED_CLOCK");
if (string.Equals(fixedClock, "1", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(fixedClock, "true", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IClock>(new FixedClock(new DateTimeOffset(2026, 01, 01, 12, 00, 00, TimeSpan.Zero)));
}
else
{
    builder.Services.AddSingleton<IClock, SystemClock>();
}

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

    // Compose nested props explicitly: modes belong to a composed child component.
    var agentPanel = new Component<DashboardAgentPanelProps>(
        new DashboardAgentPanelProps(
        Insights: Shalimar.Generated.Components.DashboardAgentPanelProps.Deferred.CrmInsights(),
        Forecast: Shalimar.Generated.Components.DashboardAgentPanelProps.Lazy.CrmForecast(),
        ActivitySse: Shalimar.Generated.Components.DashboardAgentPanelProps.Sse.CrmActivitySse(),
        ActivityExport: Shalimar.Generated.Components.DashboardAgentPanelProps.Stream.CrmActivityExport()),
        Shalimar.Generated.Behaviors.DashboardAgentPanelProps.PrefetchDeferred());

    var props = new DashboardProps(
        Message: "Welcome to Shalimar",
        OpenTasks: open,
        OverdueTasks: overdue,
        Accounts: snapshot.Accounts.Count,
        FocusTasks: snapshot.Tasks.Take(5).ToList(),
        Activity: snapshot.Activity.Take(12).ToList(),
        AgentPanel: agentPanel);
    return ShalimarTypedResults.Component(MakeContext(app), props, "Shalimar App");
}).ForTsxFile("Features/Home/route.tsx").AsComponent<DashboardProps>();

// Deferred mode: resolves after hydration via typed handle in props.
app.MapGet("/crm/insights", (CrmRepository repo) => repo.Insights())
    .ForComponent<DashboardAgentPanelProps>()
    .AsDeferred<CrmInsightsDto>();

// Lazy mode: resolves only on user intent via typed handle in props.
app.MapGet("/crm/forecast", (CrmRepository repo) => repo.Forecast())
    .ForComponent<DashboardAgentPanelProps>()
    .AsLazy<CrmForecastDto>();

// SSE subscription mode (realtime / long-lived). Separate from Streamed mode.
app.MapGet("/crm/activity/sse", async (HttpContext http, CrmRepository repo, CancellationToken ct) =>
{
    http.Response.Headers.CacheControl = "no-cache";
    http.Response.Headers.Connection = "keep-alive";
    http.Response.Headers.ContentType = "text/event-stream";

    // Deterministic first event for tests + UX.
    var first = new CrmSseEventDto(
        Type: "activity",
        Activity: repo.Activity().FirstOrDefault() ?? new ActivityItemDto("act_boot", DateTimeOffset.UtcNow, "system", "Stream connected."),
        Ts: DateTimeOffset.UtcNow);

    await WriteSseAsync(http, first, ct);

    // A few deterministic ticks, then keep-alives.
    for (var i = 0; i < 2 && !ct.IsCancellationRequested; i++)
    {
        await Task.Delay(250, ct);
        var tick = new CrmSseEventDto(
            Type: "tick",
            Activity: new ActivityItemDto($"act_tick_{i + 1}", DateTimeOffset.UtcNow, "system", $"Stream tick {i + 1}"),
            Ts: DateTimeOffset.UtcNow);
        await WriteSseAsync(http, tick, ct);
    }

    while (!ct.IsCancellationRequested)
    {
        await Task.Delay(2000, ct);
        // keep-alive comment (ignored by EventSource)
        await http.Response.WriteAsync($": keep-alive {DateTimeOffset.UtcNow:O}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }

    return Results.Empty;
})
    .ForComponent<DashboardAgentPanelProps>()
    .AsSse<CrmSseEventDto>();

// Streamed mode (finite): NDJSON over HTTP using an IAsyncEnumerable-like writer.
app.MapGet("/crm/activity/export", async (HttpContext http, CrmRepository repo, CancellationToken ct) =>
{
    http.Response.Headers.CacheControl = "no-cache";
    http.Response.Headers.ContentType = "application/x-ndjson";

    var baseItems = repo.Activity().ToList();
    if (baseItems.Count == 0)
        baseItems.Add(new ActivityItemDto("act_boot", DateTimeOffset.UtcNow, "system", "Export started."));

    var isTesting = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SHALIMAR_TESTING"));
    var count = isTesting ? 60 : 20;
    var delayMs = isTesting ? 80 : 40; // keep abort deterministic in tests

    for (var i = 0; i < count && !ct.IsCancellationRequested; i++)
    {
        var item = baseItems[i % baseItems.Count];
        await WriteNdjsonAsync(http, new CrmActivityExportRowDto(i + 1, item), ct);
        await Task.Delay(delayMs, ct); // simulate large export work
    }

    return Results.Empty;
})
    .ForComponent<DashboardAgentPanelProps>()
    .AsStream<CrmActivityExportRowDto>();

app.MapGet("/tasks", async (HttpContext http, CrmRepository repo) =>
{
    var grid = new Component<TasksGridProps>(new TasksGridProps(
        TasksGrid: Shalimar.Generated.Components.TasksGridProps.Deferred.CrmTasksGrid()));
    var props = new TasksProps("Tasks", repo.Snapshot(), grid);
    return ShalimarTypedResults.Component(MakeContext(app), props, "Shalimar App");
}).ForTsxFile("Features/Tasks/route.tsx").AsComponent<TasksProps>();

// v2: server-authored tree + pure TSX renderer (route module is generated under Generated/V2Routes/**).
app.MapGet("/v2/workbench", async (HttpContext http, CrmRepository repo) =>
{
    var agentPanel = new Component<WorkbenchAgentPanelProps>(
        new WorkbenchAgentPanelProps(
            Insights: Shalimar.Generated.Components.WorkbenchProps.Deferred.V2WorkbenchAgentInsights()),
        Shalimar.Generated.Behaviors.WorkbenchProps.PrefetchDeferred());

    var props = new WorkbenchProps(
        Title: "V2 Workbench",
        Summary: Shalimar.Generated.Components.WorkbenchProps.Deferred.V2WorkbenchSummary(),
        AgentPanel: agentPanel);

    return ShalimarTypedResults.Component(MakeContext(app), props, "Shalimar App");
}).ForTsxFile("Features/V2/Workbench/WorkbenchPage.tsx").AsComponent<WorkbenchProps>();

app.MapGet("/v2/workbench/summary", (CrmRepository repo) =>
{
    var snapshot = repo.Snapshot();
    var open = snapshot.Tasks.Count(t => t.Status != "done");
    var overdue = snapshot.Tasks.Count(t => t.DueAt is not null && t.DueAt.Value < DateTimeOffset.UtcNow && t.Status != "done");
    return new WorkbenchSummaryDto(OpenTasks: open, OverdueTasks: overdue, ActiveAgents: 3);
})
    .ForComponent<WorkbenchProps>()
    .ForNode<WorkbenchProps>(p => p.Summary)
    .AsDeferred<WorkbenchSummaryDto>();

app.MapGet("/v2/workbench/agent/insights", (CrmRepository repo) =>
{
    return new AgentInsightsDto(
        Headline: "Next best actions",
        Suggestions: new[] { "Triage overdue tasks", "Draft follow-up to account owner", "Prepare weekly status update" });
})
    .ForComponent<WorkbenchProps>()
    .ForNode<WorkbenchProps>(p => p.AgentPanel.Props.Insights)
    .AsDeferred<AgentInsightsDto>();

static async Task WriteSseAsync<T>(HttpContext http, T data, CancellationToken ct)
{
    // Default JSON options are camelCase in Shalimar, but here we keep it explicit for SSE payloads.
    var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    });

    await http.Response.WriteAsync("data: ", ct);
    await http.Response.WriteAsync(json, ct);
    await http.Response.WriteAsync("\n\n", ct);
    await http.Response.Body.FlushAsync(ct);
}

static async Task WriteNdjsonAsync<T>(HttpContext http, T data, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    });

    await http.Response.WriteAsync(json, ct);
    await http.Response.WriteAsync("\n", ct);
    await http.Response.Body.FlushAsync(ct);
}

app.MapGet("/tasks/board", async (HttpContext http, CrmRepository repo) =>
{
    var props = new TaskBoardProps("Board", repo.Snapshot());
    return ShalimarTypedResults.Component(MakeContext(app), props, "Shalimar App");
}).ForTsxFile("Features/Tasks/Board/route.tsx").AsComponent<TaskBoardProps>();

app.MapGet("/tasks/{taskId}", async (HttpContext http, string taskId) =>
{
    var props = new TaskDetailProps("Task", taskId);
    return ShalimarTypedResults.Component(MakeContext(app), props, "Shalimar App");
}).ForTsxFile("Features/Tasks/$taskId/route.tsx").AsComponent<TaskDetailProps>();

app.MapGet("/accounts", async (HttpContext http, CrmRepository repo) =>
{
    var props = new AccountsProps("Accounts", repo.Snapshot());
    return ShalimarTypedResults.Component(MakeContext(app), props, "Shalimar App");
}).ForTsxFile("Features/Accounts/route.tsx").AsComponent<AccountsProps>();

app.MapGet("/accounts/{accountId}", async (HttpContext http, string accountId, CrmRepository repo) =>
{
    var account = repo.Accounts.FirstOrDefault(a => a.Id == accountId);
    var contacts = repo.ContactsByAccount(accountId);
    var owner = account is null ? null : repo.Users.FirstOrDefault(u => u.Id == account.OwnerId);
    var props = new AccountDetailProps("Account", accountId, account, contacts, owner);
    return ShalimarTypedResults.Component(MakeContext(app), props, "Shalimar App");
}).ForTsxFile("Features/Accounts/$accountId/route.tsx").AsComponent<AccountDetailProps>();

app.MapGet("/settings", async (HttpContext http) =>
{
    var props = new SettingsProps("Settings", app.Environment.EnvironmentName);
    return ShalimarTypedResults.Component(MakeContext(app), props, "Shalimar App");
}).ForTsxFile("Features/Settings/route.tsx").AsComponent<SettingsProps>();

// Shalimar-style "feature endpoints" (no /api). These will be replaced by generated hooks later.
app.MapGet("/crm/snapshot", (CrmRepository repo) => Results.Ok(repo.Snapshot()));
app.MapGet("/crm/tasks/grid", (HttpRequest req, CrmRepository repo) =>
{
    static int GetInt(HttpRequest req, string key, int fallback)
    {
        var raw = req.Query[key].ToString();
        return int.TryParse(raw, out var v) ? v : fallback;
    }

    static string? GetString(HttpRequest req, string key)
    {
        var raw = req.Query[key].ToString();
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    var page = GetInt(req, "page", 1);
    var pageSize = GetInt(req, "pageSize", 20);
    var q = GetString(req, "q");
    var status = GetString(req, "status");
    var priority = GetString(req, "priority");
    var epicId = GetString(req, "epicId");

    return Results.Ok(repo.TasksGrid(page, pageSize, q, status, priority, epicId));
})
    .ForComponent<TasksGridProps>()
    .AsDeferred<CrmTasksGridDto>();
app.MapGet("/crm/tasks/{taskId}/messages", (CrmRepository repo, string taskId) => Results.Ok(repo.Messages(taskId)));
app.MapGet("/crm/tasks/{taskId}/artifacts", (CrmRepository repo, string taskId) => Results.Ok(repo.Artifacts(taskId)));
app.MapGet("/crm/tasks/{taskId}/decisions", (CrmRepository repo, string taskId) => Results.Ok(repo.Decisions(taskId)));
app.MapGet("/crm/accounts/{accountId}/contacts", (CrmRepository repo, string accountId) => Results.Ok(repo.ContactsByAccount(accountId)));

// Test-only escape hatch: deterministic state reset for Playwright suites.
if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SHALIMAR_TESTING")))
{
    app.MapPost("/crm/reset", (CrmRepository repo) =>
    {
        repo.Reset();
        return Results.Ok(new { ok = true });
    });
}

app.MapPost("/crm/tasks", async (CrmRepository repo, IValidator<CreateTaskRequest> v, CreateTaskRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    return Results.Ok(repo.CreateTask(req));
})
    .Invalidates<DashboardAgentPanelProps>()
    .Invalidates<TasksGridProps>()
    .AsMutation<CreateTaskRequest, TaskDto>();

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
