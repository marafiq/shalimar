namespace Shalimar.SandboxApp.Features.V2.Observations;

using Shalimar;
using Shalimar.SandboxApp.Features.SeniorLiving;

public sealed record ObservationsProps(
    string Title,
    string? Query,
    int Page,
    int PageSize,
    string? Pane, // "new" or observation id
    Deferred<ObservationsGridDto> Grid) : IComponentProps;

public sealed record DeleteObservationRequest();
public sealed record DeleteObservationResult(bool Ok);

