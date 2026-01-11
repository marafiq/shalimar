using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shalimar;
using Xunit;

namespace Shalimar.Runtime.Tests;

public class ShellRendererTests
{
    [Fact]
    public async Task RenderAsync_InDevelopment_IncludesViteClientAndLocalEntryScript()
    {
        var env = new FakeWebHostEnvironment { EnvironmentName = Environments.Development };
        var renderer = new ShellRenderer(env);

        var html = await renderer.RenderAsync(new { environment = "Development" }, new { message = "hi" });

        Assert.Contains("src=\"/@vite/client\"", html);
        Assert.Contains("src=\"/Features/App/main.tsx\"", html);
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Test";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "/tmp/wwwroot";
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = "/tmp";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

