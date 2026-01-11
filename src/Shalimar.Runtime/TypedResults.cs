using Microsoft.AspNetCore.Http;

namespace Shalimar;

/// <summary>
/// Shalimar-specific typed results for Minimal APIs.
/// The goal is to keep server contracts explicit and composable (no magic strings),
/// while still being fully interoperable with standard Minimal API endpoints.
/// </summary>
public static class ShalimarTypedResults
{
    public static ComponentResult<TContext, TProps> Component<TContext, TProps>(
        TContext context,
        TProps props,
        string title = "Shalimar App",
        string version = "1.0.0")
        where TContext : class
        => new(context, props, title, version);
}

/// <summary>
/// Result for rendering a Shalimar component shell.
/// </summary>
public sealed class ComponentResult<TContext, TProps> : IResult
    where TContext : class
{
    private readonly TContext _context;
    private readonly TProps _props;
    private readonly string _title;
    private readonly string _version;

    public ComponentResult(TContext context, TProps props, string title, string version)
    {
        _context = context;
        _props = props;
        _title = title;
        _version = version;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        // Delegate to the canonical renderer. This keeps the contract centralized.
        var result = await httpContext.RenderComponent(_context, _props, _title, _version);
        await result.ExecuteAsync(httpContext);
    }
}

