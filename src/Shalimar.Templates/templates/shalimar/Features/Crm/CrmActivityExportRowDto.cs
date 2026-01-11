namespace ShalimarApp.Features.Crm;

/// <summary>
/// Streamed export row for NDJSON streaming (finite).
/// </summary>
public sealed record CrmActivityExportRowDto(
    int Index,
    ActivityItemDto Activity);

