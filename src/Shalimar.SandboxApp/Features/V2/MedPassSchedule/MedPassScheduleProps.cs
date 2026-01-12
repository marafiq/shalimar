namespace Shalimar.SandboxApp.Features.V2.MedPassSchedule;

using Shalimar;
using Shalimar.SandboxApp.Features.SeniorLiving;

public sealed record MedPassScheduleProps(
    string Title,
    string? Query,
    int Page,
    int PageSize,
    string? Pane, // "new" or schedule id
    Deferred<MedPassScheduleGridDto> Grid) : IComponentProps;

public sealed record DeleteMedPassScheduleRequest();
public sealed record DeleteMedPassScheduleResult(bool Ok);

