namespace ShalimarApp.Features.V2.Live;

using Shalimar;

public sealed record V2LiveProps(
    string Title,
    Deferred<LiveSummaryDto> Summary,
    Lazy<LiveTimelineDto> Timeline,
    Component<LiveRealtimePanelProps> RealtimePanel) : IComponentProps;

public sealed record LiveRealtimePanelProps(
    Stream<LiveAuditEventDto> Audit,
    Sse<LiveNotificationEventDto> Notifications) : IComponentProps;

public sealed record LiveSummaryDto(
    int OpenTasks,
    int OverdueTasks,
    int ActiveAgents,
    int UnreadNotifications);

public sealed record LiveTimelineDto(
    IReadOnlyList<LiveTimelineItemDto> Items);

public sealed record LiveTimelineItemDto(
    string Id,
    DateTimeOffset Ts,
    string Kind,
    string Summary);

public sealed record LiveAuditEventDto(
    int Seq,
    DateTimeOffset Ts,
    string Message);

public sealed record LiveNotificationEventDto(
    string Id,
    DateTimeOffset Ts,
    string Title,
    string Body);

