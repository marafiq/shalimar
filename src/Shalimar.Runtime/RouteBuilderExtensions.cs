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
