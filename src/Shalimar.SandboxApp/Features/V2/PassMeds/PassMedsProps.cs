namespace Shalimar.SandboxApp.Features.V2.PassMeds;

using Shalimar;
using Shalimar.SandboxApp.Features.SeniorLiving;

public sealed record PassMedsProps(
    string Title,
    string? Query,
    Deferred<PassMedsDueDto> Due,
    Deferred<PassMedsRecentDto> Recent) : IComponentProps;

