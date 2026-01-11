namespace ShalimarApp.Features.Crm;

/// <summary>
/// Typed SSE event envelope. Separate from Streamed mode.
/// </summary>
public sealed record CrmSseEventDto(
    string Type,
    ActivityItemDto Activity,
    DateTimeOffset Ts);

