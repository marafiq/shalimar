namespace Shalimar;

/// <summary>
/// Marker contract for Shalimar component props.
/// Props are server-owned and should be composed explicitly (including nested components and modes).
/// </summary>
public interface IComponentProps { }

/// <summary>
/// Explicit composition wrapper: allows a parent props model to embed a child component's props,
/// including the child's Deferred/Lazy/Stream/SSE handles, without magic strings.
/// </summary>
public sealed record Component<TProps>(TProps Props, ShalimarBehaviors? Behaviors = null) where TProps : IComponentProps;

