using ShalimarApp.Features.Crm;

namespace ShalimarApp.Features.Tasks.Board;

// Server is truth: this props model represents what the Board component needs.
public sealed record TaskBoardProps(
    string Message,
    CrmSnapshot Crm) : Shalimar.IComponentProps;

