using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using System.Linq.Expressions;

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
        where TProps : IComponentProps
    {
        // Store metadata for source generator to process
        builder.WithMetadata(new ShalimarComponentMetadata(typeof(TProps)));
        return builder;
    }

    /// <summary>
    /// Associates an endpoint (Deferred/Lazy/Stream/SSE) with a parent component props type.
    /// This enables generator output to be organized as a component tree.
    /// </summary>
    public static RouteHandlerBuilder ForComponent<TProps>(this RouteHandlerBuilder builder)
        where TProps : IComponentProps
    {
        builder.WithMetadata(new ShalimarForComponentMetadata(typeof(TProps)));
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
    /// Marks this endpoint as an SSE subscription (realtime / long-lived).
    /// SSE is separate from Streamed mode.
    /// </summary>
    public static RouteHandlerBuilder AsSse<T>(this RouteHandlerBuilder builder)
    {
        builder.WithMetadata(new ShalimarSseMetadata(typeof(T)));
        return builder;
    }

    /// <summary>
    /// Marks this endpoint as a JSON mutation (command) with request/response types.
    /// </summary>
    public static RouteHandlerBuilder AsMutation<TRequest, TResponse>(this RouteHandlerBuilder builder)
    {
        builder.WithMetadata(new ShalimarMutationMetadata(typeof(TRequest), typeof(TResponse)));
        return builder;
    }

    /// <summary>
    /// Declares that this endpoint invalidates a component's derived data.
    /// This is used by the generator to emit typed invalidation helpers.
    /// </summary>
    public static RouteHandlerBuilder Invalidates<TProps>(this RouteHandlerBuilder builder)
        where TProps : IComponentProps
    {
        builder.WithMetadata(new ShalimarInvalidatesMetadata(typeof(TProps)));
        return builder;
    }

    /// <summary>
    /// Explicitly binds a TSX module to render this route/component.
    /// This is the canonical way to bind server routes to client renderers (no conventions, no magic).
    /// </summary>
    public static RouteHandlerBuilder ForTsxFile(this RouteHandlerBuilder builder, string path)
    {
        builder.WithMetadata(new ShalimarTsxFileMetadata(path));
        return builder;
    }

    /// <summary>
    /// Binds a mode endpoint to a specific leaf node inside the component props tree.
    /// This is how the generator knows "where does this Deferred/Lazy/Stream/Sse leaf load from?".
    /// </summary>
    public static RouteHandlerBuilder ForNode<TProps>(this RouteHandlerBuilder builder, Expression<Func<TProps, object?>> node)
        where TProps : IComponentProps
    {
        builder.WithMetadata(new ShalimarNodeMetadata(typeof(TProps), CanonicalizeNodePath(node)));
        return builder;
    }

    private static string CanonicalizeNodePath<TProps>(Expression<Func<TProps, object?>> expr)
    {
        // Accept expressions like:
        //   p => p.AgentPanel.Insights
        //   p => p.AgentPanel.Props.Insights   (we ignore the wrapper's Props segment)
        //   p => (object)p.AgentPanel.Insights (box/conversion)

        Expression body = expr.Body;
        while (body is UnaryExpression u && (u.NodeType == ExpressionType.Convert || u.NodeType == ExpressionType.ConvertChecked))
            body = u.Operand;

        var segments = new Stack<string>();
        while (body is MemberExpression m)
        {
            // Drop Component<T>.Props to keep paths stable (AgentPanel.Insights, not AgentPanel.Props.Insights).
            if (!string.Equals(m.Member.Name, "Props", StringComparison.Ordinal))
                segments.Push(m.Member.Name);
            body = m.Expression!;
        }

        if (segments.Count == 0)
            throw new InvalidOperationException("ForNode requires a member-access expression like p => p.X.Y.Z");

        return string.Join(".", segments);
    }
}

/// <summary>
/// Metadata indicating this route is a Shalimar component.
/// </summary>
public record ShalimarComponentMetadata(Type PropsType);

/// <summary>
/// Metadata for the TSX file path.
/// </summary>
public record ShalimarTsxFileMetadata(string Path);

/// <summary>
/// Metadata binding a mode endpoint to a leaf node path for a component props type.
/// </summary>
public record ShalimarNodeMetadata(Type ComponentPropsType, string NodePath);

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

/// <summary>
/// Metadata indicating this endpoint is a Shalimar SSE subscription.
/// </summary>
public record ShalimarSseMetadata(Type EventType);

/// <summary>
/// Metadata indicating this endpoint belongs to a component props model.
/// </summary>
public record ShalimarForComponentMetadata(Type ComponentPropsType);

/// <summary>
/// Metadata indicating this endpoint is a Shalimar mutation (request/response).
/// </summary>
public record ShalimarMutationMetadata(Type RequestType, Type ResponseType);

/// <summary>
/// Metadata indicating this endpoint invalidates a component.
/// Multiple invalidations can be attached.
/// </summary>
public record ShalimarInvalidatesMetadata(Type ComponentPropsType);
