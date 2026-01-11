namespace ShalimarApp.Features.Crm;

public sealed record CrmTasksGridDto(
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<TaskDto> Items);

