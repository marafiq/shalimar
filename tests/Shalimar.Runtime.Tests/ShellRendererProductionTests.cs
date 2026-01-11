using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shalimar;
using Xunit;

namespace Shalimar.Runtime.Tests;

public class ShellRendererProductionTests
{
    [Fact]
    public async Task RenderAsync_InProduction_UsesManifestHashedAssets()
    {
        using var temp = new TempDir();
        var wwwroot = temp.Path;
        Directory.CreateDirectory(Path.Combine(wwwroot, "dist", ".vite"));

        await File.WriteAllTextAsync(
            Path.Combine(wwwroot, "dist", ".vite", "manifest.json"),
            """
            {
              "Features/App/main.tsx": {
                "file": "assets/main-abc123.js",
                "css": ["assets/main-abc123.css"],
                "isEntry": true
              }
            }
            """);

        var env = new FakeWebHostEnvironment
        {
            EnvironmentName = Environments.Production,
            WebRootPath = wwwroot
        };

        var renderer = new ShellRenderer(env);
        var html = await renderer.RenderAsync(new { environment = "Production" }, new { message = "hi" });

        Assert.Contains("href=\"/dist/assets/main-abc123.css\"", html);
        Assert.Contains("src=\"/dist/assets/main-abc123.js\"", html);
    }

    [Fact]
    public async Task RenderAsync_InProduction_Throws_WhenManifestMissing()
    {
        using var temp = new TempDir();
        var env = new FakeWebHostEnvironment
        {
            EnvironmentName = Environments.Production,
            WebRootPath = temp.Path
        };

        var renderer = new ShellRenderer(env);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => renderer.RenderAsync(new { environment = "Production" }, new { message = "hi" }));

        Assert.Contains("Vite manifest not found", ex.Message);
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Test";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "/tmp/wwwroot";
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ContentRootPath { get; set; } = "/tmp";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "shalimar-tests", Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}

