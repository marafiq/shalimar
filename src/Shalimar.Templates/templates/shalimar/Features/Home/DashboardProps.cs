using ShalimarApp.Features.Crm;
using Shalimar;

namespace ShalimarApp.Features.Home;

// Server is truth: this props model represents what the Dashboard component needs.
public sealed record DashboardProps(
    string Message,
    int OpenTasks,
    int OverdueTasks,
    int Accounts,
    IReadOnlyList<TaskDto> FocusTasks,
    IReadOnlyList<ActivityItemDto> Activity,
    Component<DashboardAgentPanelProps> AgentPanel) : IComponentProps;

// Nested (composed) component props: keeps modes grouped and composable.
public sealed record DashboardAgentPanelProps(
    Deferred<CrmInsightsDto> Insights,
    Lazy<CrmForecastDto> Forecast,
    Sse<CrmSseEventDto> ActivitySse,
    Stream<CrmActivityExportRowDto> ActivityExport) : IComponentProps;

