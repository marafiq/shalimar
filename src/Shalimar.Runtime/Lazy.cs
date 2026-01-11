namespace Shalimar;

/// <summary>
/// Represents a lazy data reference that is resolved only when triggered by the user.
/// The server remains the source of truth; the client uses <see cref="Href"/> to fetch JSON.
/// </summary>
public sealed record Lazy<T>(string Href);

