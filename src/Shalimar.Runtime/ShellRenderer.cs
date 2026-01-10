using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Shalimar;

/// <summary>
/// Renders the HTML shell for Shalimar applications.
/// Handles Vite manifest parsing, asset injection, and context/props serialization.
/// </summary>
public class ShellRenderer
{
    private readonly IWebHostEnvironment _environment;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _cachedManifest;
    private Dictionary<string, ViteManifestEntry>? _manifestEntries;

    public ShellRenderer(IWebHostEnvironment environment, JsonSerializerOptions? jsonOptions = null)
    {
        _environment = environment;
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Renders the HTML shell with context and props injected.
    /// </summary>
    public async Task<string> RenderAsync<TContext, TProps>(
        TContext context,
        TProps props,
        string title = "Shalimar App",
        string version = "1.0.0")
        where TContext : class
    {
        var scriptPath = await GetMainScriptPathAsync();
        var cssLinks = await GetCssLinksAsync();

        var contextJson = JsonSerializer.Serialize(context, _jsonOptions);
        var propsJson = JsonSerializer.Serialize(props, _jsonOptions);

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>{EscapeHtml(title)}</title>
                {cssLinks}
            </head>
            <body>
                <div id="root"></div>
                <script>
                    window.__SHALIMAR_CONTEXT__ = {contextJson};
                    window.__SHALIMAR_VERSION__ = "{EscapeJs(version)}";
                    window.__SHALIMAR_PROPS__ = {propsJson};
                </script>
                <script type="module" src="{scriptPath}"></script>
            </body>
            </html>
            """;
    }

    /// <summary>
    /// Gets the main script path from Vite manifest (production) or dev server (development).
    /// </summary>
    public async Task<string> GetMainScriptPathAsync(string entryPoint = "Client/main.tsx")
    {
        if (_environment.IsDevelopment())
        {
            // In development, use Vite dev server
            return $"http://localhost:5173/{entryPoint}";
        }

        // In production, read from manifest
        var manifest = await LoadManifestAsync();
        if (manifest != null && manifest.TryGetValue(entryPoint, out var entry))
        {
            return $"/dist/{entry.File}";
        }

        // Fallback
        return "/dist/assets/main.js";
    }

    /// <summary>
    /// Gets CSS links from Vite manifest for production builds.
    /// </summary>
    public async Task<string> GetCssLinksAsync(string entryPoint = "Client/main.tsx")
    {
        if (_environment.IsDevelopment())
        {
            // Vite dev server handles CSS injection
            return "";
        }

        var manifest = await LoadManifestAsync();
        if (manifest == null || !manifest.TryGetValue(entryPoint, out var entry) || entry.Css == null)
        {
            return "";
        }

        var links = entry.Css.Select(css => $"<link rel=\"stylesheet\" href=\"/dist/{css}\">");
        return string.Join("\n    ", links);
    }

    private async Task<Dictionary<string, ViteManifestEntry>?> LoadManifestAsync()
    {
        if (_manifestEntries != null)
            return _manifestEntries;

        var manifestPath = Path.Combine(_environment.WebRootPath, "dist", ".vite", "manifest.json");
        if (!File.Exists(manifestPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath);
            if (json == _cachedManifest && _manifestEntries != null)
                return _manifestEntries;

            _cachedManifest = json;
            _manifestEntries = JsonSerializer.Deserialize<Dictionary<string, ViteManifestEntry>>(json);
            return _manifestEntries;
        }
        catch
        {
            return null;
        }
    }

    private static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    private static string EscapeJs(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }

    private class ViteManifestEntry
    {
        public string File { get; set; } = "";
        public string[]? Css { get; set; }
        public string[]? Assets { get; set; }
        public bool IsEntry { get; set; }
    }
}
