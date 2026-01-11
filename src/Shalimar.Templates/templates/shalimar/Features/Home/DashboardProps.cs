using ShalimarApp.Features.Crm;

namespace ShalimarApp.Features.Home;

// Server is truth: this props model represents what the Dashboard component needs.
public sealed record DashboardProps(
    string Message,
    int OpenTasks,
    int OverdueTasks,
    int Accounts,
    IReadOnlyList<TaskDto> FocusTasks,
    IReadOnlyList<ActivityItemDto> Activity);

