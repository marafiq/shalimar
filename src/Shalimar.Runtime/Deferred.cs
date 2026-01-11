namespace Shalimar;

/// <summary>
/// Represents a deferred data reference that is resolved client-side after hydration.
/// The server remains the source of truth; the client uses <see cref="Href"/> to fetch JSON.
/// </summary>
public sealed record Deferred<T>(string Href);

