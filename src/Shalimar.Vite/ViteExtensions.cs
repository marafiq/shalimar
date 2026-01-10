using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http;

namespace Shalimar.Vite;

/// <summary>
/// Extension methods for Vite dev server integration.
/// </summary>
public static class ViteExtensions
{
    /// <summary>
    /// Adds Vite development server integration services.
    /// </summary>
    public static IServiceCollection AddShalimarVite(this IServiceCollection services, ViteOptions? options = null)
    {
        options ??= new ViteOptions();
        services.AddSingleton(options);
        services.AddHttpClient("ViteDevServer", client =>
        {
            client.BaseAddress = new Uri($"http://localhost:{options.DevServerPort}");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }

    /// <summary>
    /// Configures the application to proxy HMR and asset requests to Vite dev server in development.
    /// </summary>
    public static IApplicationBuilder UseShalimarVite(this IApplicationBuilder app, IHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            var options = app.ApplicationServices.GetService<ViteOptions>() ?? new ViteOptions();
            app.UseMiddleware<ViteDevServerMiddleware>(options);
        }
        return app;
    }
}

/// <summary>
/// Configuration options for Vite dev server integration.
/// </summary>
public class ViteOptions
{
    /// <summary>
    /// The port where Vite dev server is running. Default: 5173
    /// </summary>
    public int DevServerPort { get; set; } = 5173;

    /// <summary>
    /// Paths that should be proxied to Vite dev server.
    /// </summary>
    public string[] ProxyPaths { get; set; } = new[]
    {
        "/@vite",
        "/@fs",
        "/@id",
        "/@react-refresh",
        "/node_modules",
        "/Client",
        "/Features",
        "/Generated",
        "/__vite_ping"
    };
}

/// <summary>
/// Middleware that proxies requests to Vite dev server during development.
/// </summary>
public class ViteDevServerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ViteOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public ViteDevServerMiddleware(
        RequestDelegate next,
        ViteOptions options,
        IHttpClientFactory httpClientFactory)
    {
        _next = next;
        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Check if this request should be proxied to Vite
        if (ShouldProxyToVite(path))
        {
            await ProxyToVite(context);
            return;
        }

        await _next(context);
    }

    private bool ShouldProxyToVite(string path)
    {
        // Proxy HMR websocket
        if (path == "/__vite_ping")
            return true;

        // Proxy known Vite paths
        foreach (var proxyPath in _options.ProxyPaths)
        {
            if (path.StartsWith(proxyPath, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Proxy .tsx, .ts, .jsx, .js files that aren't in wwwroot
        if (path.EndsWith(".tsx") || path.EndsWith(".ts") ||
            path.EndsWith(".jsx") || path.EndsWith(".js") ||
            path.EndsWith(".css"))
        {
            // Don't proxy files from /dist (production builds)
            if (!path.StartsWith("/dist/"))
                return true;
        }

        return false;
    }

    private async Task ProxyToVite(HttpContext context)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ViteDevServer");
            var requestUri = $"http://localhost:{_options.DevServerPort}{context.Request.Path}{context.Request.QueryString}";

            using var request = new HttpRequestMessage(
                new HttpMethod(context.Request.Method),
                requestUri);

            // Copy headers
            foreach (var header in context.Request.Headers)
            {
                if (!header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            // Copy body for POST/PUT requests
            if (context.Request.ContentLength > 0)
            {
                request.Content = new StreamContent(context.Request.Body);
                if (context.Request.ContentType != null)
                {
                    request.Content.Headers.ContentType =
                        System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
                }
            }

            using var response = await client.SendAsync(request);

            context.Response.StatusCode = (int)response.StatusCode;

            // Copy response headers
            foreach (var header in response.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
            foreach (var header in response.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            // Remove headers that might cause issues
            context.Response.Headers.Remove("transfer-encoding");

            await response.Content.CopyToAsync(context.Response.Body);
        }
        catch (HttpRequestException)
        {
            // Vite dev server not running, return 503
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync(
                "Vite dev server is not running. Start it with 'bun run dev' or 'npm run dev'.");
        }
    }
}
