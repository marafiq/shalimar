using Shalimar;
using ShalimarApp.Features.Crm;

namespace ShalimarApp.Features.Tasks;

// Nested component: server-owned props for the tasks grid (paged + filterable).
public sealed record TasksGridProps(Deferred<CrmTasksGridDto> TasksGrid) : IComponentProps;

