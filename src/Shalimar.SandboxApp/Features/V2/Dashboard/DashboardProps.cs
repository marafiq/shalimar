namespace Shalimar.SandboxApp.Features.V2.Dashboard;

using Shalimar;
using Shalimar.SandboxApp.Features.SeniorLiving;

public sealed record DashboardProps(
    string Title,
    DashboardCountsDto Counts,
    Lazy<DashboardResidentsDto> Residents,
    Lazy<DashboardIncidentsDto> Incidents,
    Lazy<DashboardObservationsDto> Observations) : IComponentProps;

public sealed record DashboardResidentsDto(IReadOnlyList<ResidentDto> Items);
public sealed record DashboardIncidentsDto(IReadOnlyList<IncidentDto> Items);
public sealed record DashboardObservationsDto(IReadOnlyList<ObservationDto> Items);

