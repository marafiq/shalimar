namespace Shalimar.SandboxApp.Features.V2.Incidents;

using Shalimar;
using Shalimar.SandboxApp.Features.SeniorLiving;

public sealed record IncidentsProps(
    string Title,
    string? Query,
    int Page,
    int PageSize,
    string? Pane, // "new" or incident id
    Deferred<IncidentsGridDto> Grid) : IComponentProps;

public sealed record DeleteIncidentRequest();
public sealed record DeleteIncidentResult(bool Ok);

