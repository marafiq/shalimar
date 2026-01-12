using Shalimar;
using Shalimar.Vite;
using Shalimar.SandboxApp.Features.V2.Welcome;
using Shalimar.SandboxApp.Features.SeniorLiving;
using FluentValidation;
using Shalimar.SandboxApp.Features.V2.Dashboard;
using Shalimar.SandboxApp.Features.V2.Residents;
using Shalimar.SandboxApp.Features.V2.Incidents;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddShalimar<AppContextModel>(options =>
{
    options.ContextFactory = (sp, http) => new AppContextModel
    {
        Environment = builder.Environment.EnvironmentName
    };
});

builder.Services.AddShalimarVite();

builder.Services.AddSingleton<SeniorLivingRepository>();
builder.Services.AddSingleton<IValidator<CreateResidentRequest>, CreateResidentRequestValidator>();
builder.Services.AddSingleton<IValidator<UpdateResidentRequest>, UpdateResidentRequestValidator>();
builder.Services.AddSingleton<IValidator<CreateIncidentRequest>, CreateIncidentRequestValidator>();
builder.Services.AddSingleton<IValidator<UpdateIncidentRequest>, UpdateIncidentRequestValidator>();

var app = builder.Build();
app.UseShalimar();
app.UseShalimarVite(app.Environment);

static AppContextModel MakeContext(WebApplication app) => new() { Environment = app.Environment.EnvironmentName };
static IDictionary<string, string[]> ToValidationProblem(FluentValidation.Results.ValidationResult result) =>
    result.Errors
        .GroupBy(e => e.PropertyName)
        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

app.MapGet("/", (HttpContext http) =>
{
    var props = new WelcomeProps("Welcome to Shalimar");
    return ShalimarTypedResults.Component(MakeContext(app), props, "Shalimar");
}).ForTsxFile("Features/V2/Welcome/WelcomePage.tsx").AsComponent<WelcomeProps>();

app.MapGet("/dashboard", (SeniorLivingRepository repo) =>
{
    var counts = repo.Counts();
    var props = new DashboardProps(
        Title: "Dashboard",
        Counts: counts,
        Residents: Shalimar.Generated.Components.DashboardProps.Lazy.DashboardResidents(),
        Incidents: Shalimar.Generated.Components.DashboardProps.Lazy.DashboardIncidents(),
        Observations: Shalimar.Generated.Components.DashboardProps.Lazy.DashboardObservations());

    return ShalimarTypedResults.Component(MakeContext(app), props, "Shalimar");
}).ForTsxFile("Features/V2/Dashboard/DashboardPage.tsx").AsComponent<DashboardProps>();

app.MapGet("/dashboard/residents", (SeniorLivingRepository repo) => new DashboardResidentsDto(repo.ResidentsPreview()))
    .ForComponent<DashboardProps>()
    .ForNode<DashboardProps>(p => p.Residents)
    .AsLazy<DashboardResidentsDto>();

app.MapGet("/dashboard/incidents", (SeniorLivingRepository repo) => new DashboardIncidentsDto(repo.IncidentsPreview()))
    .ForComponent<DashboardProps>()
    .ForNode<DashboardProps>(p => p.Incidents)
    .AsLazy<DashboardIncidentsDto>();

app.MapGet("/dashboard/observations", (SeniorLivingRepository repo) => new DashboardObservationsDto(repo.ObservationsPreview()))
    .ForComponent<DashboardProps>()
    .ForNode<DashboardProps>(p => p.Observations)
    .AsLazy<DashboardObservationsDto>();

static string BuildQueryHref(string path, (string Key, string? Value)[] items)
{
    var parts = items
        .Where(i => !string.IsNullOrWhiteSpace(i.Value))
        .Select(i => $"{Uri.EscapeDataString(i.Key)}={Uri.EscapeDataString(i.Value!)}")
        .ToList();
    return parts.Count == 0 ? path : path + "?" + string.Join("&", parts);
}

app.MapGet("/residents", (HttpRequest req, SeniorLivingRepository repo) =>
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

    var q = GetString(req, "q");
    var page = GetInt(req, "page", 1);
    var pageSize = GetInt(req, "pageSize", 20);
    var pane = GetString(req, "pane");

    var href = BuildQueryHref(
        "/residents/grid",
        new[]
        {
            ("q", q),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()),
        });

    var props = new ResidentsProps(
        Title: "Residents",
        Query: q,
        Page: page,
        PageSize: pageSize,
        Pane: pane,
        Grid: new Deferred<ResidentsGridDto>(href));

    return ShalimarTypedResults.Component(MakeContext(app), props, "Shalimar");
}).ForTsxFile("Features/V2/Residents/ResidentsPage.tsx").AsComponent<ResidentsProps>();

app.MapGet("/residents/grid", (HttpRequest req, SeniorLivingRepository repo) =>
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

    var q = GetString(req, "q");
    var page = GetInt(req, "page", 1);
    var pageSize = GetInt(req, "pageSize", 20);

    return repo.ResidentsGrid(page, pageSize, q);
})
    .ForComponent<ResidentsProps>()
    .ForNode<ResidentsProps>(p => p.Grid)
    .AsDeferred<ResidentsGridDto>();

app.MapPost("/residents", async (SeniorLivingRepository repo, IValidator<CreateResidentRequest> v, CreateResidentRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    return Results.Ok(repo.CreateResident(req));
}).AsMutation<CreateResidentRequest, ResidentDto>();

app.MapPatch("/residents/{id}", async (SeniorLivingRepository repo, IValidator<UpdateResidentRequest> v, string id, UpdateResidentRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    var updated = repo.UpdateResident(id, req);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
}).AsMutation<UpdateResidentRequest, ResidentDto>();

app.MapPost("/residents/{id}/delete", (SeniorLivingRepository repo, string id, DeleteResidentRequest req) =>
{
    var ok = repo.DeleteResident(id);
    return Results.Ok(new DeleteResidentResult(ok));
}).AsMutation<DeleteResidentRequest, DeleteResidentResult>();

app.MapGet("/incidents", (HttpRequest req, SeniorLivingRepository repo) =>
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

    var q = GetString(req, "q");
    var page = GetInt(req, "page", 1);
    var pageSize = GetInt(req, "pageSize", 20);
    var pane = GetString(req, "pane");

    var href = BuildQueryHref(
        "/incidents/grid",
        new[]
        {
            ("q", q),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()),
        });

    var props = new IncidentsProps(
        Title: "Incidents",
        Query: q,
        Page: page,
        PageSize: pageSize,
        Pane: pane,
        Grid: new Deferred<IncidentsGridDto>(href));

    return ShalimarTypedResults.Component(MakeContext(app), props, "Shalimar");
}).ForTsxFile("Features/V2/Incidents/IncidentsPage.tsx").AsComponent<IncidentsProps>();

app.MapGet("/incidents/grid", (HttpRequest req, SeniorLivingRepository repo) =>
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

    var q = GetString(req, "q");
    var page = GetInt(req, "page", 1);
    var pageSize = GetInt(req, "pageSize", 20);

    return repo.IncidentsGrid(page, pageSize, q);
})
    .ForComponent<IncidentsProps>()
    .ForNode<IncidentsProps>(p => p.Grid)
    .AsDeferred<IncidentsGridDto>();

app.MapPost("/incidents", async (SeniorLivingRepository repo, IValidator<CreateIncidentRequest> v, CreateIncidentRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    return Results.Ok(repo.CreateIncident(req));
}).AsMutation<CreateIncidentRequest, IncidentDto>()
  .Invalidates<IncidentsProps>()
  .Invalidates<DashboardProps>();

app.MapPatch("/incidents/{id}", async (SeniorLivingRepository repo, IValidator<UpdateIncidentRequest> v, string id, UpdateIncidentRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    var updated = repo.UpdateIncident(id, req);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
}).AsMutation<UpdateIncidentRequest, IncidentDto>()
  .Invalidates<IncidentsProps>()
  .Invalidates<DashboardProps>();

app.MapPost("/incidents/{id}/delete", (SeniorLivingRepository repo, string id, DeleteIncidentRequest req) =>
{
    var ok = repo.DeleteIncident(id);
    return Results.Ok(new DeleteIncidentResult(ok));
}).AsMutation<DeleteIncidentRequest, DeleteIncidentResult>()
  .Invalidates<IncidentsProps>()
  .Invalidates<DashboardProps>();

if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SHALIMAR_TESTING")))
{
    app.MapPost("/test/reset", (SeniorLivingRepository repo) =>
    {
        repo.Reset();
        return Results.Ok(new { ok = true });
    });
}

app.Run();

public sealed record AppContextModel
{
    public required string Environment { get; init; }
}

