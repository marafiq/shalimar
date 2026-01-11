namespace Shalimar;

/// <summary>
/// Represents an SSE subscription reference (realtime / long-lived).
/// This is NOT the same as Streamed (finite) mode.
/// </summary>
public sealed record Sse<T>(string Href);

