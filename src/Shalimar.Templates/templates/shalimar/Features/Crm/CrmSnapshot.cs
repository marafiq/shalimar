namespace ShalimarApp.Features.Crm;

public sealed record CrmSnapshot(
    IReadOnlyList<UserDto> Users,
    IReadOnlyList<AccountDto> Accounts,
    IReadOnlyList<EpicDto> Epics,
    IReadOnlyList<TaskDto> Tasks,
    IReadOnlyList<ActivityItemDto> Activity);

