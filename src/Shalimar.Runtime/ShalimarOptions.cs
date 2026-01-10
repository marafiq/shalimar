using Microsoft.AspNetCore.Http;

namespace Shalimar;

/// <summary>
/// Configuration options for Shalimar framework.
/// </summary>
public class ShalimarOptions<TContext> where TContext : class
{
    /// <summary>
    /// Factory function to create the application context for each request.
    /// </summary>
    public Func<IServiceProvider, HttpContext, TContext>? ContextFactory { get; set; }
}
