namespace Shalimar;

/// <summary>
/// Server-owned behavior plan for a composed component subtree.
/// This is intentionally explicit and extendable: apps can ignore it, customize it, or generate it.
/// </summary>
public sealed record ShalimarBehaviors(
    IReadOnlyList<string> DeferredHrefs,
    IReadOnlyList<string> LazyHrefs,
    IReadOnlyList<string> StreamedHrefs,
    IReadOnlyList<string> SseHrefs);

