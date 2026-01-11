namespace ShalimarApp.Features.V2.Tasks;

using Shalimar;
using ShalimarApp.Features.Crm;

public sealed record V2TasksProps(
    string Title,
    CrmSnapshot Crm,
    Deferred<CrmTasksGridDto> Grid) : IComponentProps;

