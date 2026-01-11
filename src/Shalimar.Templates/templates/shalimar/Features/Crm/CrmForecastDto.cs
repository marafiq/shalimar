namespace ShalimarApp.Features.Crm;

/// <summary>
/// Lazy dashboard forecast (server-owned). Loaded only on user intent.
/// </summary>
public sealed record CrmForecastDto(
    string Summary,
    IReadOnlyList<string> Drivers,
    IReadOnlyList<string> RecommendedPlays);

