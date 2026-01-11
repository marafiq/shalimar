namespace Shalimar;

/// <summary>
/// Represents a streamed data reference that is resolved via SSE.
/// The server remains the source of truth; the client uses <see cref="Href"/> to connect.
/// </summary>
public sealed record Stream<T>(string Href);

