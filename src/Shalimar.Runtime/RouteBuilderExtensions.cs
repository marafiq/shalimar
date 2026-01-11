using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Shalimar;

/// <summary>
/// Extension methods for configuring Shalimar routes.
/// </summary>
public static class RouteBuilderExtensions
{
    /// <summary>
    /// Marks this endpoint as a component route with associated props type.
    /// </summary>
    public static RouteHandlerBuilder AsComponent<TProps>(this RouteHandlerBuilder builder)
    {
        // Store metadata for source generator to process
        builder.WithMetadata(new ShalimarComponentMetadata(typeof(TProps)));
        return builder;
    }

    /// <summary>
    /// Marks this endpoint as a deferred JSON query. Deferred endpoints are intended to be
    /// referenced from component props via <see cref="Deferred{T}"/> handles.
    /// </summary>
    public static RouteHandlerBuilder AsDeferred<T>(this RouteHandlerBuilder builder)
    {
        builder.WithMetadata(new ShalimarDeferredMetadata(typeof(T)));
        return builder;
    }

    /// <summary>
    /// Marks this endpoint as a lazy JSON query. Lazy endpoints are intended to be
    /// referenced from component props via <see cref="Lazy{T}"/> handles and fetched only on demand.
    /// </summary>
    public static RouteHandlerBuilder AsLazy<T>(this RouteHandlerBuilder builder)
    {
        builder.WithMetadata(new ShalimarLazyMetadata(typeof(T)));
        return builder;
    }

    /// <summary>
    /// Marks this endpoint as a streamed (SSE) subscription.
    /// Stream endpoints are intended to be referenced from component props via <see cref="Stream{T}"/>.
    /// </summary>
    public static RouteHandlerBuilder AsStream<T>(this RouteHandlerBuilder builder)
    {
        builder.WithMetadata(new ShalimarStreamMetadata(typeof(T)));
        return builder;
    }

    /// <summary>
    /// Associates a JSX file with this component route.
    /// </summary>
    public static RouteHandlerBuilder WithJsxFile(this RouteHandlerBuilder builder, string path)
    {
        builder.WithMetadata(new ShalimarJsxFileMetadata(path));
        return builder;
    }
}

/// <summary>
/// Metadata indicating this route is a Shalimar component.
/// </summary>
public record ShalimarComponentMetadata(Type PropsType);

/// <summary>
/// Metadata for the JSX file path.
/// </summary>
public record ShalimarJsxFileMetadata(string Path);

/// <summary>
/// Metadata indicating this endpoint is a Shalimar deferred query.
/// </summary>
public record ShalimarDeferredMetadata(Type ResultType);

/// <summary>
/// Metadata indicating this endpoint is a Shalimar lazy query.
/// </summary>
public record ShalimarLazyMetadata(Type ResultType);

/// <summary>
/// Metadata indicating this endpoint is a Shalimar stream (SSE) subscription.
/// </summary>
public record ShalimarStreamMetadata(Type EventType);
