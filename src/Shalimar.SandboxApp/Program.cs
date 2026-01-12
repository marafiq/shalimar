using Shalimar;
using Shalimar.Vite;
using Shalimar.SandboxApp.Features.V2.Welcome;
using Shalimar.SandboxApp.Features.SeniorLiving;
using FluentValidation;
using Shalimar.SandboxApp.Features.V2.Dashboard;
using Shalimar.SandboxApp.Features.V2.Residents;
using Shalimar.SandboxApp.Features.V2.Incidents;
using Shalimar.SandboxApp.Features.V2.Observations;
using Shalimar.SandboxApp.Features.V2.MedPassSchedule;
using Shalimar.SandboxApp.Features.V2.PassMeds;

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
builder.Services.AddSingleton<IValidator<CreateObservationRequest>, CreateObservationRequestValidator>();
builder.Services.AddSingleton<IValidator<UpdateObservationRequest>, UpdateObservationRequestValidator>();
builder.Services.AddSingleton<IValidator<CreateMedPassScheduleRequest>, CreateMedPassScheduleRequestValidator>();
builder.Services.AddSingleton<IValidator<UpdateMedPassScheduleRequest>, UpdateMedPassScheduleRequestValidator>();
builder.Services.AddSingleton<IValidator<PassMedRequest>, PassMedRequestValidator>();

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
})
    .Invalidates<ResidentsProps>()
    .Invalidates<DashboardProps>()
    .AsMutation<CreateResidentRequest, ResidentDto>();

app.MapPatch("/residents/{id}", async (SeniorLivingRepository repo, IValidator<UpdateResidentRequest> v, string id, UpdateResidentRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    var updated = repo.UpdateResident(id, req);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
})
    .Invalidates<ResidentsProps>()
    .Invalidates<DashboardProps>()
    .AsMutation<UpdateResidentRequest, ResidentDto>();

app.MapPost("/residents/{id}/delete", (SeniorLivingRepository repo, string id, DeleteResidentRequest req) =>
{
    var ok = repo.DeleteResident(id);
    return Results.Ok(new DeleteResidentResult(ok));
})
    .Invalidates<ResidentsProps>()
    .Invalidates<DashboardProps>()
    .AsMutation<DeleteResidentRequest, DeleteResidentResult>();

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
})
    .Invalidates<IncidentsProps>()
    .Invalidates<DashboardProps>()
    .AsMutation<CreateIncidentRequest, IncidentDto>();

app.MapPatch("/incidents/{id}", async (SeniorLivingRepository repo, IValidator<UpdateIncidentRequest> v, string id, UpdateIncidentRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    var updated = repo.UpdateIncident(id, req);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
})
    .Invalidates<IncidentsProps>()
    .Invalidates<DashboardProps>()
    .AsMutation<UpdateIncidentRequest, IncidentDto>();

app.MapPost("/incidents/{id}/delete", (SeniorLivingRepository repo, string id, DeleteIncidentRequest req) =>
{
    var ok = repo.DeleteIncident(id);
    return Results.Ok(new DeleteIncidentResult(ok));
})
    .Invalidates<IncidentsProps>()
    .Invalidates<DashboardProps>()
    .AsMutation<DeleteIncidentRequest, DeleteIncidentResult>();

app.MapGet("/observations", (HttpRequest req, SeniorLivingRepository repo) =>
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
        "/observations/grid",
        new[]
        {
            ("q", q),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()),
        });

    var props = new ObservationsProps(
        Title: "Observations",
        Query: q,
        Page: page,
        PageSize: pageSize,
        Pane: pane,
        Grid: new Deferred<ObservationsGridDto>(href));

    return ShalimarTypedResults.Component(MakeContext(app), props, "Shalimar");
}).ForTsxFile("Features/V2/Observations/ObservationsPage.tsx").AsComponent<ObservationsProps>();

app.MapGet("/observations/grid", (HttpRequest req, SeniorLivingRepository repo) =>
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

    return repo.ObservationsGrid(page, pageSize, q);
})
    .ForComponent<ObservationsProps>()
    .ForNode<ObservationsProps>(p => p.Grid)
    .AsDeferred<ObservationsGridDto>();

app.MapPost("/observations", async (SeniorLivingRepository repo, IValidator<CreateObservationRequest> v, CreateObservationRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    return Results.Ok(repo.CreateObservation(req));
})
    .Invalidates<ObservationsProps>()
    .Invalidates<DashboardProps>()
    .AsMutation<CreateObservationRequest, ObservationDto>();

app.MapPatch("/observations/{id}", async (SeniorLivingRepository repo, IValidator<UpdateObservationRequest> v, string id, UpdateObservationRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    var updated = repo.UpdateObservation(id, req);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
})
    .Invalidates<ObservationsProps>()
    .Invalidates<DashboardProps>()
    .AsMutation<UpdateObservationRequest, ObservationDto>();

app.MapPost("/observations/{id}/delete", (SeniorLivingRepository repo, string id, DeleteObservationRequest req) =>
{
    var ok = repo.DeleteObservation(id);
    return Results.Ok(new DeleteObservationResult(ok));
})
    .Invalidates<ObservationsProps>()
    .Invalidates<DashboardProps>()
    .AsMutation<DeleteObservationRequest, DeleteObservationResult>();

app.MapGet("/medpass/schedule", (HttpRequest req, SeniorLivingRepository repo) =>
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
        "/medpass/schedule/grid",
        new[]
        {
            ("q", q),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()),
        });

    var props = new MedPassScheduleProps(
        Title: "Med pass schedule",
        Query: q,
        Page: page,
        PageSize: pageSize,
        Pane: pane,
        Grid: new Deferred<MedPassScheduleGridDto>(href));

    return ShalimarTypedResults.Component(MakeContext(app), props, "Shalimar");
}).ForTsxFile("Features/V2/MedPassSchedule/MedPassSchedulePage.tsx").AsComponent<MedPassScheduleProps>();

app.MapGet("/medpass/schedule/grid", (HttpRequest req, SeniorLivingRepository repo) =>
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

    return repo.MedPassScheduleGrid(page, pageSize, q);
})
    .ForComponent<MedPassScheduleProps>()
    .ForNode<MedPassScheduleProps>(p => p.Grid)
    .AsDeferred<MedPassScheduleGridDto>();

app.MapPost("/medpass/schedule", async (SeniorLivingRepository repo, IValidator<CreateMedPassScheduleRequest> v, CreateMedPassScheduleRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    return Results.Ok(repo.CreateMedPassSchedule(req));
})
    .Invalidates<MedPassScheduleProps>()
    .Invalidates<PassMedsProps>()
    .AsMutation<CreateMedPassScheduleRequest, MedPassScheduleRowDto>();

app.MapPatch("/medpass/schedule/{id}", async (SeniorLivingRepository repo, IValidator<UpdateMedPassScheduleRequest> v, string id, UpdateMedPassScheduleRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    var updated = repo.UpdateMedPassSchedule(id, req);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
})
    .Invalidates<MedPassScheduleProps>()
    .Invalidates<PassMedsProps>()
    .AsMutation<UpdateMedPassScheduleRequest, MedPassScheduleRowDto>();

app.MapPost("/medpass/schedule/{id}/delete", (SeniorLivingRepository repo, string id, DeleteMedPassScheduleRequest req) =>
{
    var ok = repo.DeleteMedPassSchedule(id);
    return Results.Ok(new DeleteMedPassScheduleResult(ok));
})
    .Invalidates<MedPassScheduleProps>()
    .Invalidates<PassMedsProps>()
    .AsMutation<DeleteMedPassScheduleRequest, DeleteMedPassScheduleResult>();

app.MapGet("/medpass/pass", (HttpRequest req) =>
{
    static string? GetString(HttpRequest req, string key)
    {
        var raw = req.Query[key].ToString();
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    var q = GetString(req, "q");
    var dueHref = BuildQueryHref("/medpass/pass/due", new[] { ("q", q) });
    var recentHref = BuildQueryHref("/medpass/pass/recent", Array.Empty<(string, string?)>());

    var props = new PassMedsProps(
        Title: "Pass meds",
        Query: q,
        Due: new Deferred<PassMedsDueDto>(dueHref),
        Recent: new Deferred<PassMedsRecentDto>(recentHref));

    return ShalimarTypedResults.Component(MakeContext(app), props, "Shalimar");
}).ForTsxFile("Features/V2/PassMeds/PassMedsPage.tsx").AsComponent<PassMedsProps>();

app.MapGet("/medpass/pass/due", (HttpRequest req, SeniorLivingRepository repo) =>
{
    static string? GetString(HttpRequest req, string key)
    {
        var raw = req.Query[key].ToString();
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    var q = GetString(req, "q");
    return repo.PassMedsDue(q);
})
    .ForComponent<PassMedsProps>()
    .ForNode<PassMedsProps>(p => p.Due)
    .AsDeferred<PassMedsDueDto>();

app.MapGet("/medpass/pass/recent", (SeniorLivingRepository repo) => repo.PassMedsRecent())
    .ForComponent<PassMedsProps>()
    .ForNode<PassMedsProps>(p => p.Recent)
    .AsDeferred<PassMedsRecentDto>();

app.MapPost("/medpass/pass", async (SeniorLivingRepository repo, IValidator<PassMedRequest> v, PassMedRequest req) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid) return Results.ValidationProblem(ToValidationProblem(result));
    return Results.Ok(repo.PassMed(req));
})
    .Invalidates<PassMedsProps>()
    .AsMutation<PassMedRequest, PassMedResult>();

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

