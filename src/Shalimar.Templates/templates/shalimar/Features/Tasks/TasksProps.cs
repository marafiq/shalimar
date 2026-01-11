using ShalimarApp.Features.Crm;

namespace ShalimarApp.Features.Tasks;

// Server is truth: this props model represents what the Tasks component needs.
public sealed record TasksProps(
    string Message,
    CrmSnapshot Crm);

