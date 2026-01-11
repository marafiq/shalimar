using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Shalimar;

/// <summary>
/// Extension methods to configure Shalimar in ASP.NET Core applications.
/// </summary>
public static class ShalimarExtensions
{
    /// <summary>
    /// Adds Shalimar services to the dependency injection container.
    /// </summary>
    public static IServiceCollection AddShalimar<TContext>(
        this IServiceCollection services,
        Action<ShalimarOptions<TContext>>? configure = null) where TContext : class
    {
        var options = new ShalimarOptions<TContext>();
        configure?.Invoke(options);
        services.AddSingleton(options);

        // Register ShellRenderer as scoped (uses IWebHostEnvironment)
        services.AddScoped<ShellRenderer>(sp =>
        {
            var env = sp.GetRequiredService<IWebHostEnvironment>();
            return new ShellRenderer(env, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        });

        return services;
    }

    /// <summary>
    /// Configures the application to use Shalimar middleware.
    /// </summary>
    public static IApplicationBuilder UseShalimar(this IApplicationBuilder app)
    {
        app.UseStaticFiles();
        return app;
    }

    /// <summary>
    /// Renders an HTML shell with the specified context and props.
    /// This is a convenience method for rendering Shalimar components.
    /// </summary>
    public static async Task<IResult> RenderComponent<TContext, TProps>(
        this HttpContext httpContext,
        TContext context,
        TProps props,
        string title = "Shalimar App",
        string version = "1.0.0")
        where TContext : class
        where TProps : IComponentProps
    {
        var shellRenderer = httpContext.RequestServices.GetRequiredService<ShellRenderer>();
        var html = await shellRenderer.RenderAsync(context, props, title, version);
        return Results.Content(html, "text/html");
    }
}
