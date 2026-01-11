namespace ShalimarApp.Features.Crm;

/// <summary>
/// Typed stream event envelope for SSE. Keep this stable and explicit.
/// </summary>
public sealed record CrmStreamEventDto(
    string Type,
    ActivityItemDto Activity,
    DateTimeOffset Ts);

