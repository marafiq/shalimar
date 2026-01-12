namespace Shalimar.SandboxApp.Features.V2.Residents;

using Shalimar;
using Shalimar.SandboxApp.Features.SeniorLiving;

public sealed record ResidentsProps(
    string Title,
    string? Query,
    int Page,
    int PageSize,
    string? Pane, // "new" or resident id
    Deferred<ResidentsGridDto> Grid) : IComponentProps;

public sealed record DeleteResidentRequest();
public sealed record DeleteResidentResult(bool Ok);

