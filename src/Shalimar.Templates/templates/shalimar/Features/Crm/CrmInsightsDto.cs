namespace ShalimarApp.Features.Crm;

/// <summary>
/// Deferred dashboard insights (server-owned). This models the kind of "agent assist" panel that
/// should load after hydration via Shalimar Deferred mode.
/// </summary>
public sealed record CrmInsightsDto(
    string Summary,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> NextActions);

