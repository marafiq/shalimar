namespace ShalimarApp.Features.V2.Workbench;

using Shalimar;

public sealed record WorkbenchProps(
    string Title,
    Deferred<WorkbenchSummaryDto> Summary,
    Component<WorkbenchAgentPanelProps> AgentPanel) : IComponentProps;

public sealed record WorkbenchSummaryDto(
    int OpenTasks,
    int OverdueTasks,
    int ActiveAgents);

public sealed record WorkbenchAgentPanelProps(
    Deferred<AgentInsightsDto> Insights) : IComponentProps;

public sealed record AgentInsightsDto(
    string Headline,
    IReadOnlyList<string> Suggestions);
